import { Component, DestroyRef, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CargoOffer, ShipownerBid, VoyageTerms } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';

interface ComparisonRow { label: string; original: string; values: string[]; }

@Component({
  selector: 'of-comparative', standalone: true, imports: [RouterLink, DatePipe],
  templateUrl: './comparative.component.html', styleUrl: './comparative.component.scss'
})
export class ComparativeComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(OceanFixtureApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  offer?: CargoOffer;
  loading = true;
  saving = false;
  error = '';
  get canSelect(): boolean { return !this.loading && !this.saving && this.offer?.stage === 'CargoOrder' && !!this.offer.recapVersion; }
  get bids(): ShipownerBid[] { return this.offer?.bids ?? []; }

  constructor() { this.reload(); }

  reload(): void {
    if (this.saving) return;
    this.loading = true;
    this.error = '';
    this.api.recap(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.offer = offer; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Unable to load comparison. Please try again.'; }
    });
  }

  selectFirmOffer(bid: ShipownerBid): void {
    if (!this.canSelect || !this.offer?.recapVersion || !this.bids.some(item => item.id === bid.id)) return;
    this.saving = true;
    this.error = '';
    this.api.selectFirmOffer(this.offer.id, bid.shipownerId, this.offer.recapVersion)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: offer => {
          this.offer = offer;
          this.saving = false;
          void this.router.navigate(['/offers', offer.id, 'negotiation']);
        },
        error: response => {
          this.saving = false;
          this.error = response.status === 409 ? 'The offer has changed. Reload before selecting.'
            : 'Firm offer was not saved. Please try again.';
        }
      });
  }

  get rows(): ComparisonRow[] {
    if (!this.offer) return [];
    const fields: [string, keyof VoyageTerms][] = [
      ['CHARTERER', 'charterer'], ['CARGO QUANTITY', 'quantity'], ['STOWAGE FACTOR', 'stowageFactor'],
      ['CARGO DESCRIPTION', 'commodity'], ['PORT/S OF LOADING', 'portOfLoading'], ['LOADING CONDITIONS', 'loadingConditions'],
      ['PORT/S OF DISCHARGE', 'portOfDischarge'], ['DISCHARGE CONDITIONS', 'dischargeConditions'], ['LAY/CAN', 'laycan'],
      ['LOADING / DISCHARGING TERMS', 'loadingDischargingTerms'], ['LAYTIME TERMS', 'laytimeTerms'],
      ['C/P FORM', 'charterPartyForm'], ['RESTRICTIONS', 'restrictions'], ['ADDITIONAL REQUIREMENTS', 'additionalRequirements'],
      ['FREIGHT', 'freight'], ['DEMURRAGE', 'demurrage'], ['DESPATCH', 'despatch'], ['VESSEL NAME', 'vesselName'], ['REMARKS', 'remarks']
    ];
    return fields.map(([label, key]) => ({ label, original: this.format(this.offer!.terms, key),
      values: this.bids.map(bid => this.format(bid.terms, key)) }));
  }

  private format(terms: VoyageTerms, key: keyof VoyageTerms): string {
    const value = terms[key];
    if (value === undefined || value === null || value === '') return '—';
    if (key === 'quantity') return `${Number(value).toLocaleString('en-US')} ${terms.quantityUnit}`;
    if (key === 'freight' || key === 'demurrage') return `${terms.freightCurrency} ${Number(value).toLocaleString('en-US')} ${key === 'freight' ? terms.freightUnit : terms.demurrageUnit}`.trim();
    return String(value);
  }
}
