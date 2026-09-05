import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'app-support',
  standalone: true,
  imports: [TranslateModule, IconComponent],
  templateUrl: './support.component.html',
  styleUrl: './support.component.scss',
})
export class SupportComponent {}
