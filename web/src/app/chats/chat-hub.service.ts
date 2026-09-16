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
  /** Thread the UI currently wants to view; drives membership sync. */
  private wantedThreadId: string | null = null;
  private connecting: Promise<void> | null = null;
  /** Serializes join/leave/reconnect so overlapping navigations cannot clobber membership. */
  private hubOps: Promise<void> = Promise.resolve();

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

  isJoinedTo(threadId: string): boolean {
    return this.isConnected() && this.activeThreadId === threadId;
  }

  async joinThread(threadId: string): Promise<void> {
    this.wantedThreadId = threadId;
    await this.enqueue(() => this.syncMembership());
  }

  async leaveThread(threadId: string): Promise<void> {
    if (this.wantedThreadId === threadId) {
      this.wantedThreadId = null;
    }
    await this.enqueue(() => this.syncMembership());
  }

  async disconnect(): Promise<void> {
    this.wantedThreadId = null;
    await this.enqueue(async () => {
      await this.syncMembership();

      if (this.connection) {
        await this.connection.stop();
        this.connection = null;
      }
    });
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

  private enqueue(op: () => Promise<void>): Promise<void> {
    const run = this.hubOps.then(op, op);
    this.hubOps = run.then(
      () => undefined,
      () => undefined,
    );
    return run;
  }

  private async syncMembership(): Promise<void> {
    // Converge active membership to wantedThreadId. Re-read wanted after every
    // await so a newer join/leave during an invoke cannot leave us joined to a
    // stale thread (or claiming isJoinedTo while absent from the hub group).
    for (;;) {
      const wanted = this.wantedThreadId;

      if (this.activeThreadId === wanted) {
        return;
      }

      if (this.activeThreadId && this.activeThreadId !== wanted) {
        const leaving = this.activeThreadId;
        await this.leaveActive(leaving);
        continue;
      }

      if (!wanted) {
        return;
      }

      await this.ensureConnected();
      if (this.wantedThreadId !== wanted) {
        continue;
      }

      await this.connection!.invoke('JoinThread', wanted);
      if (this.wantedThreadId !== wanted) {
        try {
          if (this.connection?.state === HubConnectionState.Connected) {
            await this.connection.invoke('LeaveThread', wanted);
          }
        } catch {
          // Newer sync iteration will repair membership.
        }
        continue;
      }

      this.activeThreadId = wanted;
      return;
    }
  }

  private async leaveActive(threadId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('LeaveThread', threadId);
    }

    if (this.activeThreadId === threadId) {
      this.activeThreadId = null;
    }
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

    connection.onreconnected(() => {
      // Groups are empty after reconnect; drop local active and re-join wanted.
      void this.enqueue(async () => {
        this.activeThreadId = null;
        try {
          await this.syncMembership();
        } catch {
          // REST fallback remains available; next joinThread will retry.
        }
      });
    });

    return connection;
  }
}
