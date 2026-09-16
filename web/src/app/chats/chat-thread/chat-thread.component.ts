import { DatePipe } from '@angular/common';
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
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { distinctUntilChanged, firstValueFrom, map } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { ApiErrorService } from '../../i18n/api-error.service';
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
    AlertComponent,
    ButtonComponent,
    DatePipe,
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
    // Notifications (and list → thread) can navigate /my/chats/:a → /my/chats/:b
    // while reusing this component; snapshot-only reads would keep the old thread.
    this.route.paramMap
      .pipe(
        map((params) => params.get('threadId') ?? ''),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((threadId) => {
        const previousThreadId = this.threadId;
        this.threadId = threadId;
        this.draft.set('');
        this.sendError.set(null);
        this.clearPendingAttachment();
        this.attachmentStates.set({});

        if (previousThreadId && previousThreadId !== threadId) {
          void this.chatHub.leaveThread(previousThreadId);
        }

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
      // Only use the hub when joined to this thread; otherwise the send can
      // succeed server-side while MessageReceived never reaches this client.
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

      try {
        await this.chatHub.joinThread(threadId);
        if (generation !== this.loadGeneration) {
          await this.chatHub.leaveThread(threadId);
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
