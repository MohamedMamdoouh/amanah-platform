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
  /** Thread the UI currently wants; drives serialized membership sync. */
  private wantedThreadId: string | null = null;
  private connecting: Promise<void> | null = null;
  private membershipQueue: Promise<void> = Promise.resolve();

  private readonly messageReceivedSubject = new Subject<ChatMessage>();
  private readonly threadReadOnlySubject =
    new Subject<ChatThreadReadOnlyEvent>();
  private readonly reconnectedSubject = new Subject<void>();

  readonly messageReceived$: Observable<ChatMessage> =
    this.messageReceivedSubject.asObservable();
  readonly threadReadOnly$: Observable<ChatThreadReadOnlyEvent> =
    this.threadReadOnlySubject.asObservable();
  /** Fires after automatic reconnect and membership re-sync for the wanted thread. */
  readonly reconnected$: Observable<void> =
    this.reconnectedSubject.asObservable();

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  isJoinedTo(threadId: string): boolean {
    return this.isConnected() && this.activeThreadId === threadId;
  }

  async joinThread(threadId: string): Promise<void> {
    this.wantedThreadId = threadId;
    await this.runMembership(() => this.syncMembership());
  }

  async leaveThread(threadId: string): Promise<void> {
    if (this.wantedThreadId === threadId) {
      this.wantedThreadId = null;
    }
    await this.runMembership(() => this.syncMembership());
  }

  async disconnect(): Promise<void> {
    this.wantedThreadId = null;
    await this.runMembership(async () => {
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
  ): Promise<ChatMessage> {
    // Re-sync after reconnect gaps so this connection is in the group when
    // possible; invoke still returns the persisted message for local echo.
    await this.runMembership(() => this.syncMembership());
    await this.ensureConnected();

    return await this.connection!.invoke<ChatMessage>(
      'SendMessage',
      threadId,
      body ?? null,
      attachmentId ?? null,
    );
  }

  /**
   * Converge hub group membership to wantedThreadId. Re-read wanted after every
   * await so overlapping navigations / reconnect cannot claim isJoinedTo while
   * absent from the SignalR group (which makes hub sends vanish for the sender).
   */
  private async syncMembership(): Promise<void> {
    for (;;) {
      const wanted = this.wantedThreadId;

      if (this.activeThreadId === wanted) {
        // Stale active after a hard disconnect: groups are empty and the
        // connection may be down — force a reconnect/rejoin pass.
        if (wanted && !this.isConnected()) {
          this.activeThreadId = null;
          continue;
        }
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

      if (!this.isConnected()) {
        return;
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

    if (
      this.connection &&
      (this.connection.state === HubConnectionState.Connecting ||
        this.connection.state === HubConnectionState.Reconnecting)
    ) {
      await this.waitForConnectionSettle();
      if (this.isConnected()) {
        return;
      }
    }

    this.connecting ??= this.startConnection();
    try {
      await this.connecting;
    } finally {
      this.connecting = null;
    }
  }

  private waitForConnectionSettle(): Promise<void> {
    const connection = this.connection!;
    const startedAt = Date.now();
    const maxWaitMs = 60_000;
    return new Promise((resolve) => {
      const poll = () => {
        const state = connection.state;
        if (
          state === HubConnectionState.Connected ||
          state === HubConnectionState.Disconnected ||
          Date.now() - startedAt >= maxWaitMs
        ) {
          resolve();
          return;
        }
        setTimeout(poll, 50);
      };
      poll();
    });
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
      // Groups are empty after reconnect. Clear active synchronously so
      // isJoinedTo is false until sync finishes — otherwise hub SendMessage
      // can succeed while the sender is not in the group (message vanishes).
      this.activeThreadId = null;
      void this.runMembership(async () => {
        try {
          await this.syncMembership();
          this.reconnectedSubject.next();
        } catch {
          // REST fallback remains available; next joinThread will retry.
        }
      });
    });

    connection.onclose(() => {
      // Permanent close (reconnect exhausted / stop). Drop stale membership so
      // the next syncMembership does not early-return without rejoining.
      this.activeThreadId = null;
    });

    return connection;
  }

  private runMembership<T>(operation: () => Promise<T>): Promise<T> {
    const next = this.membershipQueue.then(operation);
    this.membershipQueue = next.then(
      () => undefined,
      () => undefined,
    );
    return next;
  }
}
