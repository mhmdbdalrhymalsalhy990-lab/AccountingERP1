import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'currencyFormat',
  standalone: true
})
export class CurrencyFormatPipe implements PipeTransform {
  transform(value: number | string | null | undefined, currency: string = 'SAR'): string {
    if (value === null || value === undefined || value === '') return '0.00 ' + currency;
    const num = typeof value === 'string' ? parseFloat(value) : value;
    if (isNaN(num)) return '0.00 ' + currency;

    const formatted = num.toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });

    return `${formatted} ${currency}`;
  }
}
