import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  ChatAttachmentPresignResponse,
  ChatAttachmentUploadResponse,
  ChatMessage,
  ChatThreadDetail,
  ChatThreadListResponse,
  SendMessageRequest,
} from './models/chat.models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);

  listThreads(): Observable<ChatThreadListResponse> {
    return this.http.get<ChatThreadListResponse>(
      `${environment.apiBaseUrl}/chats`,
    );
  }

  getThread(
    threadId: string,
    options?: { before?: string; limit?: number },
  ): Observable<ChatThreadDetail> {
    let params = new HttpParams();
    if (options?.before) {
      params = params.set('before', options.before);
    }
    if (options?.limit) {
      params = params.set('limit', options.limit);
    }

    return this.http.get<ChatThreadDetail>(
      `${environment.apiBaseUrl}/chats/${threadId}`,
      { params },
    );
  }

  sendMessage(
    threadId: string,
    request: SendMessageRequest,
  ): Observable<ChatMessage> {
    return this.http.post<ChatMessage>(
      `${environment.apiBaseUrl}/chats/${threadId}/messages`,
      request,
    );
  }

  uploadAttachment(
    threadId: string,
    file: File,
  ): Observable<ChatAttachmentUploadResponse> {
    const formData = new FormData();
    formData.append('threadId', threadId);
    formData.append('photo', file);

    return this.http.post<ChatAttachmentUploadResponse>(
      `${environment.apiBaseUrl}/uploads/chat-attachment`,
      formData,
    );
  }

  getAttachmentPresignedUrl(
    attachmentId: string,
  ): Observable<ChatAttachmentPresignResponse> {
    return this.http.get<ChatAttachmentPresignResponse>(
      `${environment.apiBaseUrl}/uploads/chat-attachment/${attachmentId}/url`,
    );
  }
}
