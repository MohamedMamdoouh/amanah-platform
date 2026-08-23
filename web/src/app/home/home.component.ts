import { Component } from '@angular/core';

@Component({
  selector: 'app-home',
  standalone: true,
  template: `<main class="home"><h1>أمانة</h1><p>مرحباً بك في أمانة.</p></main>`,
  styles: `
    .home {
      padding: 2rem;
      text-align: center;
    }
  `,
})
export class HomeComponent {}
