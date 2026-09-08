import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'app-unavailable',
  standalone: true,
  imports: [TranslateModule, ButtonComponent, IconComponent],
  templateUrl: './unavailable.component.html',
  styleUrl: './unavailable.component.scss',
})
export class UnavailableComponent {}
