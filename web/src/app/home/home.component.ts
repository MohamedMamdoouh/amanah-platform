import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [TranslateModule],
  template: `
    <section class="home">
      <h1>{{ 'app.name' | translate }}</h1>
      <p>{{ 'home.welcome' | translate }}</p>
    </section>
  `,
  styles: `
    .home {
      text-align: center;
      padding: 2rem 0;
    }
  `,
})
export class HomeComponent {}
