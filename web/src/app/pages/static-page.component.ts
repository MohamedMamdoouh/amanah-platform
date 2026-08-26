import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-static-page',
  standalone: true,
  imports: [TranslateModule],
  template: `
    <article class="static-page">
      <h1>{{ titleKey | translate }}</h1>
      <p>{{ bodyKey | translate }}</p>
    </article>
  `,
  styles: `
    .static-page {
      max-width: 40rem;
      margin: 0 auto;
    }
  `,
})
export class StaticPageComponent {
  private readonly route = inject(ActivatedRoute);

  titleKey = this.route.snapshot.data['titleKey'] as string;
  bodyKey = this.route.snapshot.data['bodyKey'] as string;
}
