import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  output,
  viewChild,
} from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { environment } from '../../../environments/environment';

declare global {
  interface Window {
    turnstile?: {
      render: (
        container: HTMLElement,
        options: {
          sitekey: string;
          callback: (token: string) => void;
          'expired-callback'?: () => void;
          'error-callback'?: () => void;
        },
      ) => string;
      reset: (widgetId?: string) => void;
      remove: (widgetId: string) => void;
    };
  }
}

const TURNSTILE_SCRIPT_ID = 'cf-turnstile-script';
const TURNSTILE_SCRIPT_URL =
  'https://challenges.cloudflare.com/turnstile/v0/api.js';
const TURNSTILE_LOAD_TIMEOUT_MS = 10_000;
const TURNSTILE_POLL_INTERVAL_MS = 50;

export const DEV_CAPTCHA_TOKEN = 'dev-captcha-token';

let turnstileScriptPromise: Promise<void> | null = null;

function loadTurnstileScript(): Promise<void> {
  if (window.turnstile) {
    return Promise.resolve();
  }

  turnstileScriptPromise ??= ensureTurnstileScriptTag()
    .then(() => waitForTurnstileGlobal())
    .catch((error) => {
      turnstileScriptPromise = null;
      throw error;
    });

  return turnstileScriptPromise;
}

function ensureTurnstileScriptTag(): Promise<void> {
  if (document.getElementById(TURNSTILE_SCRIPT_ID)) {
    return Promise.resolve();
  }

  return new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.id = TURNSTILE_SCRIPT_ID;
    script.src = TURNSTILE_SCRIPT_URL;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('Failed to load Turnstile script'));
    document.head.appendChild(script);
  });
}

function waitForTurnstileGlobal(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (window.turnstile) {
      resolve();
      return;
    }

    const deadline = Date.now() + TURNSTILE_LOAD_TIMEOUT_MS;
    const intervalId = window.setInterval(() => {
      if (window.turnstile) {
        window.clearInterval(intervalId);
        resolve();
        return;
      }

      if (Date.now() >= deadline) {
        window.clearInterval(intervalId);
        reject(new Error('Turnstile failed to initialize'));
      }
    }, TURNSTILE_POLL_INTERVAL_MS);
  });
}

@Component({
  selector: 'app-turnstile-widget',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './turnstile-widget.component.html',
  styleUrl: './turnstile-widget.component.scss',
})
export class TurnstileWidgetComponent implements AfterViewInit, OnDestroy {
  readonly tokenResolved = output<string>();
  readonly tokenExpired = output<void>();
  readonly tokenErrored = output<void>();

  private readonly container = viewChild<ElementRef<HTMLElement>>('turnstileContainer');

  readonly useDevMode =
    !environment.production && !environment.turnstileSiteKey;

  private widgetId: string | null = null;
  private destroyed = false;

  ngAfterViewInit(): void {
    if (this.useDevMode) {
      this.emitTokenResolved(DEV_CAPTCHA_TOKEN);
      return;
    }

    void this.loadTurnstile().catch(() => this.emitTokenErrored());
  }

  ngOnDestroy(): void {
    this.destroyed = true;

    if (this.widgetId && window.turnstile) {
      window.turnstile.remove(this.widgetId);
      this.widgetId = null;
    }
  }

  reset(): void {
    if (this.useDevMode) {
      this.emitTokenResolved(DEV_CAPTCHA_TOKEN);
      return;
    }

    if (this.widgetId && window.turnstile) {
      window.turnstile.reset(this.widgetId);
    }
  }

  private async loadTurnstile(): Promise<void> {
    await loadTurnstileScript();

    if (this.destroyed) {
      return;
    }

    const containerEl = this.container()?.nativeElement;
    if (!containerEl || !window.turnstile) {
      this.emitTokenErrored();
      return;
    }

    this.widgetId = window.turnstile.render(containerEl, {
      sitekey: environment.turnstileSiteKey,
      callback: (token) => this.emitTokenResolved(token),
      'expired-callback': () => this.emitTokenExpired(),
      'error-callback': () => this.emitTokenErrored(),
    });
  }

  private emitTokenResolved(token: string): void {
    if (!this.destroyed) {
      this.tokenResolved.emit(token);
    }
  }

  private emitTokenExpired(): void {
    if (!this.destroyed) {
      this.tokenExpired.emit();
    }
  }

  private emitTokenErrored(): void {
    if (!this.destroyed) {
      this.tokenErrored.emit();
    }
  }
}
