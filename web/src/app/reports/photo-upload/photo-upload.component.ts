import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

export interface LocalPhoto {
  id: string;
  file: File;
  previewUrl: string;
  fileName: string;
}

const MAX_PHOTOS = 5;
const MAX_BYTES = 5 * 1024 * 1024;
const ALLOWED_TYPES = new Set([
  'image/jpeg',
  'image/png',
  'image/webp',
]);

@Component({
  selector: 'app-photo-upload',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './photo-upload.component.html',
  styleUrl: './photo-upload.component.scss',
})
export class PhotoUploadComponent {
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly disabled = input(false);
  readonly photosChange = output<File[]>();

  readonly photos = signal<LocalPhoto[]>([]);
  readonly error = signal<string | null>(null);

  readonly maxPhotos = MAX_PHOTOS;

  constructor() {
    this.destroyRef.onDestroy(() => this.revokeAllPreviews(this.photos()));
  }

  canAddMore(): boolean {
    return this.photos().length < MAX_PHOTOS;
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = input.files ? Array.from(input.files) : [];
    input.value = '';

    for (const file of files) {
      if (this.photos().length >= MAX_PHOTOS) {
        break;
      }

      const validationError = this.validateFile(file);
      if (validationError) {
        this.error.set(validationError);
        continue;
      }

      if (this.isDuplicate(file)) {
        this.error.set(this.translate.instant('reports.photos.duplicate'));
        continue;
      }

      this.addPhoto(file);
    }
  }

  removePhoto(id: string): void {
    this.error.set(null);
    const photo = this.photos().find((item) => item.id === id);
    if (photo) {
      URL.revokeObjectURL(photo.previewUrl);
    }

    const next = this.photos().filter((item) => item.id !== id);
    this.photos.set(next);
    this.photosChange.emit(next.map((item) => item.file));
  }

  private validateFile(file: File): string | null {
    if (!ALLOWED_TYPES.has(file.type)) {
      return this.translate.instant('error.upload.invalid_format');
    }

    if (file.size > MAX_BYTES) {
      return this.translate.instant('error.upload.too_large');
    }

    return null;
  }

  private isDuplicate(file: File): boolean {
    return this.photos().some(
      (photo) =>
        photo.file.name === file.name &&
        photo.file.size === file.size &&
        photo.file.lastModified === file.lastModified,
    );
  }

  private addPhoto(file: File): void {
    this.error.set(null);
    const photo: LocalPhoto = {
      id: crypto.randomUUID(),
      file,
      previewUrl: URL.createObjectURL(file),
      fileName: file.name,
    };
    const next = [...this.photos(), photo];
    this.photos.set(next);
    this.photosChange.emit(next.map((item) => item.file));
  }

  private revokeAllPreviews(photos: LocalPhoto[]): void {
    for (const photo of photos) {
      URL.revokeObjectURL(photo.previewUrl);
    }
  }
}
