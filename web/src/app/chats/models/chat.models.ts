import { ResolutionState } from '../../claims/models/claim.models';

export interface ChatThreadSummary {
  id: string;
  claimId: string;
  reportId: string;
  reportTitle: string;
  reportType: 'lost' | 'found';
  counterpartyDisplayName: string;
  createdAt: string;
  readOnlyAt?: string | null;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
}

export interface ChatThreadListResponse {
  items: ChatThreadSummary[];
}

export interface ChatMessage {
  id: string;
  threadId: string;
  senderId: string;
  senderDisplayName: string;
  body: string;
  attachmentId?: string | null;
  sentAt: string;
}

export interface ChatThreadDetail {
  id: string;
  claimId: string;
  reportId: string;
  reportTitle: string;
  reportType: 'lost' | 'found';
  reportStatus: string;
  claimStatus: string;
  counterpartyDisplayName: string;
  createdAt: string;
  readOnlyAt?: string | null;
  resolution?: ResolutionState | null;
  messages: ChatMessage[];
}

export interface SendMessageRequest {
  body?: string | null;
  attachmentId?: string | null;
}

export interface ChatAttachmentUploadResponse {
  id: string;
}

export interface ChatAttachmentPresignResponse {
  url: string;
}

export interface ChatThreadReadOnlyEvent {
  threadId: string;
  readOnlyAt: string;
}

export interface MessageAttachmentState {
  url: string | null;
  loading: boolean;
}

export interface PendingAttachment {
  id: string;
  previewUrl: string;
  fileName: string;
}
