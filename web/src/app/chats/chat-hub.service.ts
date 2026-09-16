import { Injectable, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
} from '@microsoft/signalr';
import { Observable, Subject, firstValueFrom } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { ChatMessage, ChatThreadReadOnlyEvent } from './models/chat.models';

@Injectable({ providedIn: 'root' })
export class ChatHubService {
  private readonly auth = inject(AuthService);

  private connection: HubConnection | null = null;
  private activeThreadId: string | null = null;
  private connecting: Promise<void> | null = null;

  private readonly messageReceivedSubject = new Subject<ChatMessage>();
  private readonly threadReadOnlySubject =
    new Subject<ChatThreadReadOnlyEvent>();

  readonly messageReceived$: Observable<ChatMessage> =
    this.messageReceivedSubject.asObservable();
  readonly threadReadOnly$: Observable<ChatThreadReadOnlyEvent> =
    this.threadReadOnlySubject.asObservable();

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  async joinThread(threadId: string): Promise<void> {
    await this.ensureConnected();

    if (this.activeThreadId && this.activeThreadId !== threadId) {
      await this.leaveThread(this.activeThreadId);
    }

    await this.connection!.invoke('JoinThread', threadId);
    this.activeThreadId = threadId;
  }

  async leaveThread(threadId: string): Promise<void> {
    if (
      this.connection?.state === HubConnectionState.Connected &&
      this.activeThreadId === threadId
    ) {
      await this.connection.invoke('LeaveThread', threadId);
      this.activeThreadId = null;
    }
  }

  async disconnect(): Promise<void> {
    if (this.activeThreadId) {
      await this.leaveThread(this.activeThreadId);
    }

    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  async sendMessage(
    threadId: string,
    body?: string | null,
    attachmentId?: string | null,
  ): Promise<void> {
    await this.ensureConnected();
    await this.connection!.invoke(
      'SendMessage',
      threadId,
      body ?? null,
      attachmentId ?? null,
    );
  }

  private async ensureConnected(): Promise<void> {
    if (this.isConnected()) {
      return;
    }

    this.connecting ??= this.startConnection();
    try {
      await this.connecting;
    } finally {
      this.connecting = null;
    }
  }

  private async startConnection(): Promise<void> {
    if (!this.auth.getAccessToken()) {
      await firstValueFrom(this.auth.refreshSession());
    }

    if (!this.connection) {
      this.connection = this.buildConnection();
    }

    if (this.connection.state === HubConnectionState.Disconnected) {
      await this.connection.start();
    }
  }

  private buildConnection(): HubConnection {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/chat', {
        accessTokenFactory: () => this.auth.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    connection.on('MessageReceived', (message: ChatMessage) => {
      this.messageReceivedSubject.next(message);
    });

    connection.on('ThreadReadOnly', (payload: ChatThreadReadOnlyEvent) => {
      this.threadReadOnlySubject.next(payload);
    });

    connection.onreconnected(async () => {
      if (this.activeThreadId) {
        await connection.invoke('JoinThread', this.activeThreadId);
      }
    });

    return connection;
  }
}
