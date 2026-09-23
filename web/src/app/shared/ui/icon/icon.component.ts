import { Component, input } from '@angular/core';

export type IconName =
  | 'search'
  | 'location'
  | 'calendar'
  | 'shield'
  | 'check'
  | 'chat'
  | 'bell'
  | 'camera'
  | 'chevron'
  | 'lost'
  | 'found'
  | 'menu'
  | 'close';

@Component({
  selector: 'app-icon',
  standalone: true,
  template: `
    <svg
      class="icon"
      [class.icon--sm]="size() === 'sm'"
      [class.icon--md]="size() === 'md'"
      [class.icon--lg]="size() === 'lg'"
      [attr.width]="sizePx()"
      [attr.height]="sizePx()"
      viewBox="0 0 24 24"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      @switch (name()) {
        @case ('search') {
          <circle
            cx="11"
            cy="11"
            r="7"
            stroke="currentColor"
            stroke-width="2"
          />
          <path
            d="M20 20L16 16"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        }
        @case ('location') {
          <path
            d="M12 21C12 21 19 14.5 19 9.5C19 5.91 16.09 3 12.5 3C8.91 3 6 5.91 6 9.5C6 14.5 12 21 12 21Z"
            stroke="currentColor"
            stroke-width="2"
          />
          <circle
            cx="12"
            cy="9.5"
            r="2"
            stroke="currentColor"
            stroke-width="2"
          />
        }
        @case ('calendar') {
          <rect
            x="3"
            y="5"
            width="18"
            height="16"
            rx="2"
            stroke="currentColor"
            stroke-width="2"
          />
          <path
            d="M3 9H21M8 3V7M16 3V7"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        }
        @case ('shield') {
          <path
            d="M12 3L20 6V11C20 16 16.5 19 12 21C7.5 19 4 16 4 11V6L12 3Z"
            stroke="currentColor"
            stroke-width="2"
            stroke-linejoin="round"
          />
        }
        @case ('check') {
          <path
            d="M5 12L10 17L19 7"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        }
        @case ('chat') {
          <path
            d="M4 5H20V17H8L4 21V5Z"
            stroke="currentColor"
            stroke-width="2"
            stroke-linejoin="round"
          />
        }
        @case ('bell') {
          <path
            d="M12 4C9 4 7 6 7 9V13L5 16H19L17 13V9C17 6 15 4 12 4Z"
            stroke="currentColor"
            stroke-width="2"
            stroke-linejoin="round"
          />
          <path
            d="M10 19C10 20 11 21 12 21C13 21 14 20 14 19"
            stroke="currentColor"
            stroke-width="2"
          />
        }
        @case ('camera') {
          <rect
            x="3"
            y="6"
            width="18"
            height="14"
            rx="2"
            stroke="currentColor"
            stroke-width="2"
          />
          <circle
            cx="12"
            cy="13"
            r="3"
            stroke="currentColor"
            stroke-width="2"
          />
          <path
            d="M8 6L9.5 4H14.5L16 6"
            stroke="currentColor"
            stroke-width="2"
            stroke-linejoin="round"
          />
        }
        @case ('chevron') {
          <path
            d="M9 6L15 12L9 18"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        }
        @case ('lost') {
          <circle
            cx="12"
            cy="12"
            r="8"
            stroke="currentColor"
            stroke-width="2"
          />
          <path
            d="M9.6 9.4a2.5 2.5 0 1 1 3.4 2.3c-.8.4-1.3 1-1.3 1.9"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
          <path
            d="M11.7 16.5h.01"
            stroke="currentColor"
            stroke-width="2.6"
            stroke-linecap="round"
          />
        }
        @case ('found') {
          <rect
            x="4"
            y="7"
            width="16"
            height="12"
            rx="2"
            stroke="currentColor"
            stroke-width="2"
          />
          <path
            d="M8 7V5C8 4 9 3 12 3C15 3 16 4 16 5V7"
            stroke="currentColor"
            stroke-width="2"
          />
        }
        @case ('menu') {
          <path
            d="M4 7H20M4 12H20M4 17H20"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        }
        @case ('close') {
          <path
            d="M6 6L18 18M18 6L6 18"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        }
      }
    </svg>
  `,
  styleUrl: './icon.component.scss',
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  sizePx(): number {
    switch (this.size()) {
      case 'sm':
        return 16;
      case 'lg':
        return 28;
      default:
        return 20;
    }
  }
}
