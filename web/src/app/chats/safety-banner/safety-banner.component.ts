import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'app-safety-banner',
  standalone: true,
  imports: [RouterLink, TranslateModule, IconComponent],
  templateUrl: './safety-banner.component.html',
  styleUrl: './safety-banner.component.scss',
})
export class SafetyBannerComponent {}
