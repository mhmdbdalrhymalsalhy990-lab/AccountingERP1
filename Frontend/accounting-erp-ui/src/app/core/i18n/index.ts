import { AR_TRANSLATIONS } from './ar';
import { EN_TRANSLATIONS } from './en';

export { AR_TRANSLATIONS, EN_TRANSLATIONS };

export type SupportedLanguage = 'ar' | 'en';

export const TRANSLATION_MAP: Record<SupportedLanguage, Record<string, string>> = {
  ar: AR_TRANSLATIONS,
  en: EN_TRANSLATIONS
};
