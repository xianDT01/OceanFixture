import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CargoOffer, VoyageTerms } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';

interface BidRow { key: keyof VoyageTerms; label: string; original: string; value: string; multiline?: boolean; }

@Component({ selector: 'of-shipowner-bid', standalone: true, imports: [FormsModule, RouterLink],
  templateUrl: './shipowner-bid.component.html', styleUrl: './negotiation.component.scss' })
export class ShipownerBidComponent {
  private readonly api = inject(OceanFixtureApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  offer?: CargoOffer;
  rows: BidRow[] = [];
  loading = true;
  saving = false;
  error = '';
  message = '';
  get canSubmit(): boolean { return !!this.offer?.permissions?.canSubmitBid && this.offer.stage === 'CargoOrder'
    && !!this.offer.recapVersion && !this.loading && !this.saving; }

  constructor() { this.reload(); }
  reload(): void {
    if (this.saving) return;
    this.loading = true; this.error = ''; this.message = '';
    this.offer = undefined; this.rows = [];
    this.api.recap(this.route.snapshot.paramMap.get('id') ?? '').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.load(offer); this.loading = false; },
      error: () => { this.loading = false; this.error = 'Unable to load the cargo order. Please retry.'; }
    });
  }

  private load(offer: CargoOffer): void {
    this.offer = offer;
    // The API returns only the active shipowner's own bid on this screen.
    const draft = offer.bids[0]?.terms ?? offer.terms;
    const fields: [keyof VoyageTerms, string, boolean?][] = [
      ['quantity', 'CARGO QUANTITY*'], ['quantityUnit', 'QUANTITY UNIT*'], ['stowageFactor', 'STOWAGE FACTOR'],
      ['commodity', 'CARGO DESCRIPTION*', true], ['portOfLoading', 'PORT/S OF LOADING*'], ['loadingConditions', 'LOADING CONDITIONS'],
      ['portOfDischarge', 'PORT/S OF DISCHARGE*'], ['dischargeConditions', 'DISCHARGE CONDITIONS'], ['laycan', 'LAY/CAN*'],
      ['loadingDischargingTerms', 'LOADING / DISCHARGING TERMS'], ['laytimeTerms', 'LAYTIME TERMS'], ['charterPartyForm', 'C/P FORM'],
      ['restrictions', 'RESTRICTIONS'], ['additionalRequirements', 'ADDITIONAL REQUIREMENTS', true],
      ['freight', 'FREIGHT*'], ['freightCurrency', 'FREIGHT CURRENCY*'], ['freightUnit', 'FREIGHT UNIT*'],
      ['demurrage', 'DEMURRAGE'], ['demurrageUnit', 'DEMURRAGE UNIT'], ['despatch', 'DESPATCH'], ['vesselName', 'VESSEL NAME'], ['remarks', 'REMARKS', true]
    ];
    this.rows = fields.map(([key, label, multiline]) => ({ key, label, multiline,
      original: String(offer.terms[key] ?? '') || '—', value: String(draft[key] ?? '') }));
  }

  submit(): void {
    if (!this.canSubmit || !this.offer?.recapVersion) return;
    this.error = ''; this.message = '';
    const values: Record<string, string | number | undefined> = Object.fromEntries(this.rows.map(row => [row.key, row.value]));
    for (const key of ['quantity', 'freight', 'demurrage']) {
      const raw = String(values[key]).trim();
      const value = raw === '' && key === 'demurrage' ? undefined : Number(raw);
      if (value !== undefined && (!Number.isFinite(value) || value < 0 || (key !== 'demurrage' && value === 0))) {
        this.error = 'Enter positive quantity and freight, and a non-negative demurrage. Do not use thousands separators.'; return;
      }
      values[key] = value;
    }
    for (const key of ['commodity', 'quantityUnit', 'portOfLoading', 'portOfDischarge', 'laycan', 'freightCurrency', 'freightUnit']) {
      if (!String(values[key] ?? '').trim()) { this.error = 'Complete all required cargo and freight fields.'; return; }
    }
    if (!/^[A-Za-z]{3}$/.test(String(values['freightCurrency']).trim())) { this.error = 'Use a three-letter currency code, such as EUR or USD.'; return; }
    const draft = this.offer.bids[0]?.terms ?? this.offer.terms;
    const terms = { ...draft, ...values, charterer: this.offer.terms.charterer,
      freightCurrency: String(values['freightCurrency']).trim().toUpperCase() } as VoyageTerms;
    this.saving = true;
    this.api.submitBid(this.offer.id, terms, this.offer.recapVersion).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.load(offer); this.saving = false; this.message = 'Your offer was saved and is available to the charterer.'; },
      error: response => { this.saving = false; this.error = response.status === 409
        ? 'The cargo order has changed. Your edits remain here; reload discards them and loads the latest version.'
        : response.status === 403 || response.status === 404 ? 'You cannot submit an offer for this operation.' : 'Your offer was not saved. Please try again.'; }
    });
  }
}
