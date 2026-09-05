import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'app-safety',
  standalone: true,
  imports: [RouterLink, TranslateModule, ButtonComponent, IconComponent],
  templateUrl: './safety.component.html',
  styleUrl: './safety.component.scss',
})
export class SafetyComponent {}
