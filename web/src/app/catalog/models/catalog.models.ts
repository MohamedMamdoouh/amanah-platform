export interface CategoryFieldDefinition {
  fieldKey: string;
  type: string;
  required: boolean;
  sortOrder: number;
  minLength?: number | null;
  maxLength?: number | null;
  minInt?: number | null;
  maxInt?: number | null;
  textFormat?: string | null;
}

export interface Category {
  code: string;
  sortOrder: number;
  photosPrivate: boolean;
  fieldDefinitions: CategoryFieldDefinition[];
}

export interface CategoryListResponse {
  items: Category[];
}

export interface Governorate {
  code: string;
  sortOrder: number;
}

export interface GovernorateListResponse {
  items: Governorate[];
}
