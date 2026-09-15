import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { DomainLabelService } from '../../i18n/domain-label.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeVariant } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { ListingCardComponent } from '../../shared/ui/listing-card/listing-card.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { ChatService } from '../chat.service';
import { ChatThreadSummary } from '../models/chat.models';

@Component({
  selector: 'app-my-chats',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    DatePipe,
    EmptyStateComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    RouterLink,
    TranslateModule,
  ],
  templateUrl: './my-chats.component.html',
  styleUrl: './my-chats.component.scss',
})
export class MyChatsComponent implements OnInit {
  private readonly chatService = inject(ChatService);
  private readonly translate = inject(TranslateService);
  protected readonly domainLabels = inject(DomainLabelService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly threads = signal<ChatThreadSummary[]>([]);

  ngOnInit(): void {
    void this.loadThreads();
  }

  threadLink(thread: ChatThreadSummary): string[] {
    return ['/my/chats', thread.id];
  }

  threadDate(thread: ChatThreadSummary): string {
    return thread.lastMessageAt ?? thread.createdAt;
  }

  threadSubtitle(thread: ChatThreadSummary): string {
    if (thread.lastMessagePreview) {
      return thread.lastMessagePreview;
    }
    return this.translate.instant('chats.list.no_messages');
  }

  statusLabel(thread: ChatThreadSummary): string {
    return thread.readOnlyAt
      ? this.translate.instant('chats.status.read_only')
      : this.translate.instant('chats.status.active');
  }

  statusBadgeVariant(thread: ChatThreadSummary): BadgeVariant {
    return thread.readOnlyAt ? 'neutral' : 'approved';
  }

  private async loadThreads(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const response = await firstValueFrom(this.chatService.listThreads());
      this.threads.set(response.items);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
