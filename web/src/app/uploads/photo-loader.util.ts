import { firstValueFrom, Observable } from 'rxjs';

export interface DisplayPhoto {
  id: string;
  url: string | null;
  loading: boolean;
}

export interface PhotoPresignSource {
  getPresignedUrl(id: string): Observable<{ url: string }>;
}

export function initialDisplayPhotos(
  photos: { id: string; thumbnailUrl?: string | null }[],
): DisplayPhoto[] {
  return photos.map((photo) => ({
    id: photo.id,
    url: photo.thumbnailUrl ?? null,
    loading: !photo.thumbnailUrl,
  }));
}

export async function loadDisplayPhotos(
  photos: DisplayPhoto[],
  presignService: PhotoPresignSource,
  update: (
    photoId: string,
    patch: Partial<Pick<DisplayPhoto, 'url' | 'loading'>>,
  ) => void,
): Promise<void> {
  await Promise.all(
    photos.map(async (photo) => {
      if (photo.url) {
        return;
      }

      try {
        const presign = await firstValueFrom(
          presignService.getPresignedUrl(photo.id),
        );
        update(photo.id, { url: presign.url, loading: false });
      } catch {
        update(photo.id, { loading: false });
      }
    }),
  );
}
