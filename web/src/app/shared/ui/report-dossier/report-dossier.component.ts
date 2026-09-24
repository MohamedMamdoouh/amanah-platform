import { Component, computed, inject, input, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { AppDatePipe } from '../../../i18n/app-date.pipe';
import { CatalogLabelService } from '../../../i18n/catalog-label.service';
import { DisplayPhoto } from '../../../uploads/photo-loader.util';
import { ButtonComponent } from '../button/button.component';
import { PhotoLightboxComponent } from '../photo-lightbox/photo-lightbox.component';
import { SpinnerComponent } from '../spinner/spinner.component';

export interface ReportDossierData {
  type: string;
  categoryCode: string;
  governorateCode: string;
  dateLostOrFound: string;
  description: string;
  areaText?: string | null;
  heldLocation?: string | null;
  categoryFields: Record<string, string>;
  hasReward: boolean;
  rewardAmount?: number | null;
  reporterDisplayName?: string | null;
}

@Component({
  selector: 'app-report-dossier',
  standalone: true,
  imports: [
    AppDatePipe,
    ButtonComponent,
    PhotoLightboxComponent,
    SpinnerComponent,
    TranslateModule,
  ],
  templateUrl: './report-dossier.component.html',
  styleUrl: './report-dossier.component.scss',
})
export class ReportDossierComponent {
  private readonly catalogLabels = inject(CatalogLabelService);

  readonly report = input.required<ReportDossierData>();
  readonly photos = input<DisplayPhoto[]>([]);

  readonly lightboxUrl = signal<string | null>(null);

  readonly categoryFieldEntries = computed(() =>
    Object.entries(this.report().categoryFields).sort(([a], [b]) =>
      a.localeCompare(b),
    ),
  );

  readonly hasPhotos = computed(() => this.photos().length > 0);

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  fieldLabel(fieldKey: string): string {
    return this.catalogLabels.field(this.report().categoryCode, fieldKey);
  }

  isFound(): boolean {
    return this.report().type === 'found';
  }

  openPhoto(url: string | null): void {
    if (!url) {
      return;
    }
    this.lightboxUrl.set(url);
  }

  closePhoto(): void {
    this.lightboxUrl.set(null);
  }
}
