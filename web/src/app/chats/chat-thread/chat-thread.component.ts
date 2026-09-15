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
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { ChatService } from '../chat.service';
import { ChatMessage, ChatThreadDetail } from '../models/chat.models';
import { SafetyBannerComponent } from '../safety-banner/safety-banner.component';

const MESSAGE_PAGE_SIZE = 50;

@Component({
  selector: 'app-chat-thread',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    DatePipe,
    FormsModule,
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
  private readonly auth = inject(AuthService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('messagesEnd') private messagesEnd?: ElementRef<HTMLElement>;

  readonly loading = signal(true);
  readonly loadingOlder = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  readonly sendError = signal<string | null>(null);
  readonly thread = signal<ChatThreadDetail | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly hasOlderMessages = signal(false);
  readonly draft = signal('');

  private threadId = '';
  private loadGeneration = 0;
  private shouldScrollToBottom = false;

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
        this.threadId = threadId;
        this.draft.set('');
        this.sendError.set(null);
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

  async loadOlderMessages(): Promise<void> {
    const firstMessage = this.messages()[0];
    if (!firstMessage || this.loadingOlder()) {
      return;
    }

    this.loadingOlder.set(true);
    this.sendError.set(null);

    try {
      const response = await firstValueFrom(
        this.chatService.getThread(this.threadId, {
          before: firstMessage.id,
          limit: MESSAGE_PAGE_SIZE,
        }),
      );
      this.messages.update((current) => [...response.messages, ...current]);
      this.hasOlderMessages.set(response.messages.length >= MESSAGE_PAGE_SIZE);
    } catch {
      this.sendError.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loadingOlder.set(false);
    }
  }

  async sendMessage(): Promise<void> {
    const body = this.draft().trim();
    if (!body || this.sending() || this.isReadOnly()) {
      return;
    }

    this.sending.set(true);
    this.sendError.set(null);

    try {
      const message = await firstValueFrom(
        this.chatService.sendMessage(this.threadId, { body }),
      );
      this.messages.update((current) => [...current, message]);
      this.draft.set('');
      this.shouldScrollToBottom = true;
    } catch (error) {
      this.sendError.set(
        this.apiErrors.messageFromHttpError(error, {
          conflictKey: 'chats.thread.read_only_error',
        }),
      );
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
      this.shouldScrollToBottom = true;
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
}
