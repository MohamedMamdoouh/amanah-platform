/**
 * Maps API notification deep links onto current app routes.
 *
 * Chat deep links (`/my/chats/{threadId}`) must navigate to the chat thread.
 * Do not rewrite them to `/my/claims` — that was a Phase 04 stopgap until
 * the chat UI shipped.
 */
export function resolveNotificationDeepLink(
  deepLink: string,
  notificationType: string,
): string {
  if (
    (notificationType === 'ClaimWithdrawnByClaimant' ||
      notificationType === 'NewClaimSubmitted') &&
    deepLink.startsWith('/my/reports/') &&
    !deepLink.includes('#')
  ) {
    return `${deepLink}#claims-section`;
  }

  return deepLink;
}
