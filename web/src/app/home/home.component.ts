import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService } from '../auth/auth.service';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { LogoMarkComponent } from '../shared/ui/logo-mark/logo-mark.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    TranslateModule,
    RouterLink,
    ButtonComponent,
    IconComponent,
    LogoMarkComponent,
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  readonly auth = inject(AuthService);
}
