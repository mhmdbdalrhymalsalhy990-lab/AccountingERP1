import { Injectable, signal, computed } from '@angular/core';
import { type SupportedLanguage, TRANSLATION_MAP } from '../i18n';

export type { SupportedLanguage };

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private currentLangSignal = signal<SupportedLanguage>(
    (localStorage.getItem('erp_language') as SupportedLanguage) || 'ar'
  );

  readonly currentLang = this.currentLangSignal.asReadonly();
  readonly isRtl = computed(() => this.currentLangSignal() === 'ar');

  constructor() {
    this.applyLanguage(this.currentLangSignal());
  }

  setLanguage(lang: SupportedLanguage): void {
    this.currentLangSignal.set(lang);
    localStorage.setItem('erp_language', lang);
    this.applyLanguage(lang);
  }

  toggleLanguage(): void {
    const nextLang: SupportedLanguage = this.currentLangSignal() === 'ar' ? 'en' : 'ar';
    this.setLanguage(nextLang);
  }

  translate(key: string): string {
    const lang = this.currentLangSignal();
    const dictionary = TRANSLATION_MAP[lang] || TRANSLATION_MAP.ar;
    return dictionary[key] || key;
  }

  private applyLanguage(lang: SupportedLanguage): void {
    const isRtl = lang === 'ar';
    document.documentElement.lang = lang;
    document.documentElement.dir = isRtl ? 'rtl' : 'ltr';
    document.body.dir = isRtl ? 'rtl' : 'ltr';
    if (isRtl) {
      document.body.classList.add('rtl-layout');
      document.body.classList.remove('ltr-layout');
    } else {
      document.body.classList.add('ltr-layout');
      document.body.classList.remove('rtl-layout');
    }
  }
}
