import { FormBuilder, Validators } from '@angular/forms';

import { Category, CategoryFieldDefinition } from '../../catalog/models/catalog.models';
import {
  CreateReportRequest,
  ReportType,
  UpdateReportRequest,
} from '../models/report.models';

export function parseRewardAmount(raw: unknown): number | null {
  if (typeof raw === 'number' && Number.isFinite(raw)) {
    return Math.trunc(raw);
  }

  if (typeof raw === 'string' && raw.trim().length > 0) {
    const parsed = Number.parseInt(raw, 10);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

export function buildFieldValidators(definition: CategoryFieldDefinition) {
  const validators = [];

  if (definition.required) {
    validators.push(Validators.required);
  }

  if (definition.minLength != null) {
    validators.push(Validators.minLength(definition.minLength));
  }
  if (definition.maxLength != null) {
    validators.push(Validators.maxLength(definition.maxLength));
  }

  return validators;
}

export function buildCategoryFieldsGroup(
  fb: FormBuilder,
  category: Category | null,
  existingValues: Record<string, string> = {},
) {
  const group = fb.group({});

  for (const definition of category?.fieldDefinitions ?? []) {
    const validators = buildFieldValidators(definition);
    const existing = existingValues[definition.fieldKey] ?? '';
    group.addControl(definition.fieldKey, fb.control(existing, validators));
  }

  return group;
}

export function trimCategoryFields(
  categoryFields: Record<string, unknown>,
): Record<string, string> {
  const result: Record<string, string> = {};

  for (const [key, fieldValue] of Object.entries(categoryFields)) {
    if (typeof fieldValue === 'string' && fieldValue.trim().length > 0) {
      result[key] = fieldValue.trim();
    }
  }

  return result;
}

interface ReportFormValue {
  categoryCode: string;
  title: string;
  description: string;
  dateLostOrFound: string;
  governorateCode: string;
  areaText: string;
  heldLocation: string;
  hasReward: boolean;
  rewardAmount: number | null;
  hiddenDetail: string;
  categoryFields: Record<string, unknown>;
}

export function buildCreateReportRequest(
  reportType: ReportType,
  value: ReportFormValue,
): CreateReportRequest {
  return {
    type: reportType,
    categoryCode: value.categoryCode,
    title: value.title.trim(),
    description: value.description.trim(),
    dateLostOrFound: value.dateLostOrFound,
    governorateCode: value.governorateCode,
    areaText: value.areaText.trim() || null,
    heldLocation: reportType === 'found' ? value.heldLocation.trim() : null,
    hasReward: value.hasReward,
    rewardAmount: value.hasReward
      ? parseRewardAmount(value.rewardAmount)
      : null,
    hiddenDetail: value.hiddenDetail.trim(),
    categoryFields: trimCategoryFields(value.categoryFields),
  };
}

export function buildUpdateReportRequest(
  reportType: ReportType,
  value: ReportFormValue,
): UpdateReportRequest {
  const request = buildCreateReportRequest(reportType, value);
  const { type: _type, ...updateRequest } = request;
  return updateRequest;
}
