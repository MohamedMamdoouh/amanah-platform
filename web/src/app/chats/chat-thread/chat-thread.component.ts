import {
  AfterViewChecked,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AppDatePipe } from '../../i18n/app-date.pipe';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { distinctUntilChanged, firstValueFrom, map } from 'rxjs';

import {
  AbuseFlagService,
  FlagListingResponse,
} from '../../abuse/abuse-flag.service';
import { FlagListingDialogComponent } from '../../abuse/flag-listing-dialog.component';
import { AuthService } from '../../auth/auth.service';
import { ApiErrorService } from '../../i18n/api-error.service';
import { ReportService } from '../../reports/report.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { ChatHubService } from '../chat-hub.service';
import { ChatService } from '../chat.service';
import {
  ChatMessage,
  ChatThreadDetail,
  MessageAttachmentState,
  PendingAttachment,
} from '../models/chat.models';
import { SafetyBannerComponent } from '../safety-banner/safety-banner.component';

const MESSAGE_PAGE_SIZE = 50;
const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;
const ALLOWED_ATTACHMENT_TYPES = new Set([
  'image/jpeg',
  'image/png',
  'image/webp',
]);

@Component({
  selector: 'app-chat-thread',
  standalone: true,
  imports: [
    AppDatePipe,
    AlertComponent,
    ButtonComponent,
    FlagListingDialogComponent,
    FormsModule,
    IconComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    RouterLink,
    SafetyBannerComponent,
    TranslateModule,
  ],
  templateUrl: './chat-thread.component.html',
  styleUrl: './chat-thread.component.scss',
})
export class ChatThreadComponent implements OnInit, AfterViewChecked {
  private readonly route = inject(ActivatedRoute);
  private readonly chatService = inject(ChatService);
  private readonly chatHub = inject(ChatHubService);
  private readonly auth = inject(AuthService);
  private readonly abuseFlagService = inject(AbuseFlagService);
  private readonly reportService = inject(ReportService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('messagesEnd') private messagesEnd?: ElementRef<HTMLElement>;

  readonly loading = signal(true);
  readonly loadingOlder = signal(false);
  readonly sending = signal(false);
  readonly uploadingAttachment = signal(false);
  readonly error = signal<string | null>(null);
  readonly sendError = signal<string | null>(null);
  readonly thread = signal<ChatThreadDetail | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly hasOlderMessages = signal(false);
  readonly draft = signal('');
  readonly pendingAttachment = signal<PendingAttachment | null>(null);
  readonly attachmentStates = signal<Record<string, MessageAttachmentState>>(
    {},
  );
  readonly openFlag = signal<FlagListingResponse | null>(null);
  readonly flagDialogOpen = signal(false);
  readonly flagSuccessMessage = signal<string | null>(null);
  readonly reportControlVisible = signal(false);

  private threadId = '';
  private loadGeneration = 0;
  private shouldScrollToBottom = false;

  constructor() {
    this.chatHub.messageReceived$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((message) => {
        if (message.threadId !== this.threadId) {
          return;
        }

        this.appendMessage(message);
        this.shouldScrollToBottom = true;
      });

    this.chatHub.threadReadOnly$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((payload) => {
        if (payload.threadId !== this.threadId) {
          return;
        }

        this.thread.update((current) =>
          current ? { ...current, readOnlyAt: payload.readOnlyAt } : current,
        );
      });

    this.destroyRef.onDestroy(() => {
      if (this.pendingAttachment()) {
        this.clearPendingAttachment();
      }

      void this.chatHub.leaveThread(this.threadId);
    });
  }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        map((params) => params.get('threadId') ?? ''),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((threadId) => {
        this.threadId = threadId;
        this.draft.set('');
        this.sendError.set(null);
        this.clearPendingAttachment();
        this.attachmentStates.set({});

        void this.loadThread();
      });
  }

  ngAfterViewChecked(): void {
    if (!this.shouldScrollToBottom) {
      return;
    }

    this.messagesEnd?.nativeElement.scrollIntoView({ behavior: 'smooth' });
    this.shouldScrollToBottom = false;
  }

  isReadOnly(): boolean {
    return this.thread()?.readOnlyAt != null;
  }

  isOwnMessage(message: ChatMessage): boolean {
    return message.senderId === this.auth.currentUser()?.id;
  }

  reportLink(): string[] {
    const thread = this.thread();
    if (!thread) {
      return ['/browse'];
    }
    return [`/${thread.reportType}`, thread.reportId];
  }

  reportControlLabelKey(): string {
    return this.openFlag()
      ? 'abuse.flag.view_button'
      : 'abuse.flag.chat_button';
  }

  onReportClick(): void {
    if (!this.reportControlVisible()) {
      return;
    }

    if (!this.openFlag()) {
      this.flagSuccessMessage.set(null);
    }

    this.flagDialogOpen.set(true);
  }

  closeFlagDialog(): void {
    this.flagDialogOpen.set(false);
  }

  onFlagSubmitted(flag: FlagListingResponse): void {
    this.openFlag.set(flag);
    this.flagDialogOpen.set(false);
    this.flagSuccessMessage.set(
      this.translate.instant('abuse.flag.submit_success'),
    );
  }

  canSend(): boolean {
    return (
      !this.isReadOnly() &&
      !this.sending() &&
      !this.uploadingAttachment() &&
      (this.draft().trim().length > 0 || this.pendingAttachment() != null)
    );
  }

  attachmentState(attachmentId: string): MessageAttachmentState | undefined {
    return this.attachmentStates()[attachmentId];
  }

  async loadOlderMessages(): Promise<void> {
    const firstMessage = this.messages()[0];
    if (!firstMessage || this.loadingOlder()) {
      return;
    }

    const threadId = this.threadId;
    const generation = this.loadGeneration;
    this.loadingOlder.set(true);
    this.sendError.set(null);

    try {
      const response = await firstValueFrom(
        this.chatService.getThread(threadId, {
          before: firstMessage.id,
          limit: MESSAGE_PAGE_SIZE,
        }),
      );
      if (generation !== this.loadGeneration || this.threadId !== threadId) {
        return;
      }

      this.messages.update((current) => [...response.messages, ...current]);
      this.hasOlderMessages.set(response.messages.length >= MESSAGE_PAGE_SIZE);
      this.loadAttachmentsForMessages(response.messages);
    } catch {
      if (generation !== this.loadGeneration || this.threadId !== threadId) {
        return;
      }

      this.sendError.set(this.translate.instant('error.internal.error'));
    } finally {
      if (generation === this.loadGeneration) {
        this.loadingOlder.set(false);
      }
    }
  }

  async onPhotoSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file || this.isReadOnly() || this.pendingAttachment()) {
      return;
    }

    const validationError = this.validateAttachmentFile(file);
    if (validationError) {
      this.sendError.set(validationError);
      return;
    }

    this.uploadingAttachment.set(true);
    this.sendError.set(null);

    try {
      const upload = await firstValueFrom(
        this.chatService.uploadAttachment(this.threadId, file),
      );
      this.pendingAttachment.set({
        id: upload.id,
        previewUrl: URL.createObjectURL(file),
        fileName: file.name,
      });
    } catch (error) {
      this.sendError.set(
        this.apiErrors.messageFromHttpError(error, {
          fallbackKey: 'chats.thread.attachment_upload_error',
        }),
      );
    } finally {
      this.uploadingAttachment.set(false);
    }
  }

  clearPendingAttachment(): void {
    const pending = this.pendingAttachment();
    if (pending) {
      URL.revokeObjectURL(pending.previewUrl);
    }
    this.pendingAttachment.set(null);
  }

  async onAttachmentImageError(attachmentId: string): Promise<void> {
    try {
      const presign = await firstValueFrom(
        this.chatService.getAttachmentPresignedUrl(attachmentId),
      );
      this.patchAttachmentState(attachmentId, {
        url: presign.url,
        loading: false,
      });
    } catch {
      // Silent refresh failure — image stays broken until next load.
    }
  }

  async sendMessage(): Promise<void> {
    const body = this.draft().trim();
    const attachment = this.pendingAttachment();
    if ((!body && !attachment) || !this.canSend()) {
      return;
    }

    this.sending.set(true);
    this.sendError.set(null);

    const threadId = this.threadId;

    try {
      if (this.chatHub.isJoinedTo(threadId)) {
        await this.chatHub.sendMessage(
          threadId,
          body || null,
          attachment?.id ?? null,
        );
      } else {
        const message = await firstValueFrom(
          this.chatService.sendMessage(threadId, {
            body: body || null,
            attachmentId: attachment?.id ?? null,
          }),
        );
        this.appendMessage(message);
        this.shouldScrollToBottom = true;
      }

      this.draft.set('');
      this.clearPendingAttachment();
    } catch (error) {
      this.sendError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.sending.set(false);
    }
  }

  private async loadThread(): Promise<void> {
    const threadId = this.threadId;
    const generation = ++this.loadGeneration;

    if (!threadId) {
      this.error.set(this.translate.instant('error.internal.error'));
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.resetFlagState();

    try {
      const response = await firstValueFrom(
        this.chatService.getThread(threadId, {
          limit: MESSAGE_PAGE_SIZE,
        }),
      );
      if (generation !== this.loadGeneration) {
        return;
      }

      this.thread.set(response);
      this.messages.set(response.messages);
      this.hasOlderMessages.set(response.messages.length >= MESSAGE_PAGE_SIZE);
      this.loadAttachmentsForMessages(response.messages);
      this.shouldScrollToBottom = true;
      void this.loadFlagContext(
        response.reportId,
        response.reportStatus,
        generation,
        threadId,
      );

      try {
        if (generation !== this.loadGeneration) {
          return;
        }

        await this.chatHub.joinThread(threadId);
        if (generation !== this.loadGeneration) {
          // Newer navigation may already want a different thread; restore that
          // instead of clearing membership (which drops realtime for the open thread).
          if (this.threadId) {
            await this.chatHub.joinThread(this.threadId);
          } else {
            await this.chatHub.leaveThread(threadId);
          }
        }
      } catch {
        // REST fallback remains available when the hub is unavailable.
      }
    } catch (error) {
      if (generation !== this.loadGeneration) {
        return;
      }

      if (this.apiErrors.extractBody(error)) {
        this.error.set(this.translate.instant('chats.thread.not_found'));
      } else {
        this.error.set(this.translate.instant('error.internal.error'));
      }
    } finally {
      if (generation === this.loadGeneration) {
        this.loading.set(false);
      }
    }
  }

  private resetFlagState(): void {
    this.openFlag.set(null);
    this.flagDialogOpen.set(false);
    this.flagSuccessMessage.set(null);
    this.reportControlVisible.set(false);
  }

  private async loadFlagContext(
    reportId: string,
    reportStatus: string,
    generation: number,
    threadId: string,
  ): Promise<void> {
    const isOwner = await this.checkListingOwnership(reportId);
    if (!this.isCurrentLoad(generation, threadId)) {
      return;
    }

    if (isOwner) {
      return;
    }

    let openFlag: FlagListingResponse | null = null;
    try {
      openFlag = await firstValueFrom(
        this.abuseFlagService.getOpenFlag(reportId),
      );
    } catch {
      openFlag = null;
    }

    if (!this.isCurrentLoad(generation, threadId)) {
      return;
    }

    const flaggable =
      reportStatus === 'published' || reportStatus === 'claim_in_progress';

    if (openFlag) {
      this.openFlag.set(openFlag);
      this.reportControlVisible.set(true);
      return;
    }

    if (flaggable) {
      this.reportControlVisible.set(true);
    }
  }

  private isCurrentLoad(generation: number, threadId: string): boolean {
    return generation === this.loadGeneration && this.threadId === threadId;
  }

  private async checkListingOwnership(reportId: string): Promise<boolean> {
    try {
      await firstValueFrom(this.reportService.getById(reportId));
      return true;
    } catch {
      return false;
    }
  }

  private appendMessage(message: ChatMessage): void {
    this.messages.update((current) => {
      if (current.some((item) => item.id === message.id)) {
        return current;
      }

      return [...current, message];
    });

    if (message.attachmentId) {
      void this.loadAttachmentUrl(message.attachmentId);
    }
  }

  private loadAttachmentsForMessages(messages: ChatMessage[]): void {
    for (const message of messages) {
      if (message.attachmentId) {
        void this.loadAttachmentUrl(message.attachmentId);
      }
    }
  }

  private async loadAttachmentUrl(attachmentId: string): Promise<void> {
    const existing = this.attachmentStates()[attachmentId];
    if (existing?.url) {
      return;
    }

    this.patchAttachmentState(attachmentId, { url: null, loading: true });

    try {
      const presign = await firstValueFrom(
        this.chatService.getAttachmentPresignedUrl(attachmentId),
      );
      this.patchAttachmentState(attachmentId, {
        url: presign.url,
        loading: false,
      });
    } catch {
      this.patchAttachmentState(attachmentId, { url: null, loading: false });
    }
  }

  private patchAttachmentState(
    attachmentId: string,
    patch: MessageAttachmentState,
  ): void {
    this.attachmentStates.update((current) => ({
      ...current,
      [attachmentId]: patch,
    }));
  }

  private validateAttachmentFile(file: File): string | null {
    if (!ALLOWED_ATTACHMENT_TYPES.has(file.type)) {
      return this.translate.instant('error.upload.invalid_format');
    }

    if (file.size > MAX_ATTACHMENT_BYTES) {
      return this.translate.instant('error.upload.too_large');
    }

    return null;
  }
}
