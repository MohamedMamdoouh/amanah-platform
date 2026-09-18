import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AccountService } from '../../settings/account.service';
import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-reactivate-account',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    PageHeaderComponent,
    TranslateModule,
  ],
  templateUrl: './reactivate-account.component.html',
  styleUrl: './reactivate-account.component.scss',
})
export class ReactivateAccountComponent {
  private readonly accountService = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly router = inject(Router);
  readonly reactivating = signal(false);
  readonly error = signal<string | null>(null);

  async reactivate(): Promise<void> {
    if (this.reactivating()) {
      return;
    }

    this.reactivating.set(true);
    this.error.set(null);

    try {
      await firstValueFrom(this.accountService.reactivateAccount());
      await firstValueFrom(this.auth.fetchCurrentUser());
      await this.router.navigate(['/']);
    } catch (err) {
      this.error.set(this.apiErrors.messageFromHttpError(err));
    } finally {
      this.reactivating.set(false);
    }
  }

  async cancel(): Promise<void> {
    try {
      await firstValueFrom(this.auth.logout());
    } catch {
      this.auth.clearSession();
    }

    await this.router.navigate(['/login']);
  }
}
