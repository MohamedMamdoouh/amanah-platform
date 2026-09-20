import { Component, inject, input } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';

export type CategoryFieldFormGroup = FormGroup<{
  fieldKey: FormControl<string>;
  required: FormControl<boolean>;
  sortOrder: FormControl<number>;
  minLength: FormControl<string>;
  maxLength: FormControl<string>;
  textFormat: FormControl<string>;
}>;

@Component({
  selector: 'app-category-field-form',
  standalone: true,
  imports: [FormFieldComponent, ReactiveFormsModule, TranslateModule],
  templateUrl: './category-field-form.component.html',
  styleUrl: './category-field-form.component.scss',
})
export class CategoryFieldFormComponent {
  private readonly translate = inject(TranslateService);

  readonly form = input.required<CategoryFieldFormGroup>();

  readonly textFormats = ['', 'letters_and_spaces'];

  textFormatLabel(format: string | null | undefined): string {
    if (!format) {
      return this.translate.instant('admin.categories.text_format.none');
    }
    return this.translate.instant(`admin.categories.text_format.${format}`);
  }
}
