import {
  Component,
  HostListener,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-photo-lightbox',
  standalone: true,
  imports: [IconComponent, TranslateModule],
  templateUrl: './photo-lightbox.component.html',
  styleUrl: './photo-lightbox.component.scss',
})
export class PhotoLightboxComponent {
  private readonly document = inject(DOCUMENT);

  readonly src = input.required<string>();
  readonly closed = output<void>();

  constructor() {
    effect((onCleanup) => {
      this.src();
      const previous = this.document.body.style.overflow;
      this.document.body.style.overflow = 'hidden';
      onCleanup(() => {
        this.document.body.style.overflow = previous;
      });
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closed.emit();
    }
  }
}
