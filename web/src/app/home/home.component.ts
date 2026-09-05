import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { ButtonComponent } from '../shared/ui/button/button.component';
import { IconComponent } from '../shared/ui/icon/icon.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [TranslateModule, ButtonComponent, IconComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {}
