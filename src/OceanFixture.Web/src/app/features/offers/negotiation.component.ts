import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CargoOffer, VoyageTerms } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';

interface NegotiationRow { key: keyof VoyageTerms; label: string; original: string; value: string; multiline?: boolean; }

@Component({
  selector: 'of-negotiation', standalone: true, imports: [FormsModule, RouterLink],
  templateUrl: './negotiation.component.html', styleUrl: './negotiation.component.scss'
})
export class NegotiationComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(OceanFixtureApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  offer?: CargoOffer;
  loading = true;
  saving = false;
  error = '';
  message = '';
  rows: NegotiationRow[] = [];
  chartererClauses: string[] = [];
  shipownerClauses: string[] = [];
  get counter(): number { return this.offer?.counter ?? 0; }
  get turn(): string { return this.offer?.negotiationTurn ?? ''; }
  get hasChanges(): boolean {
    return !!this.offer && (this.rows.some(row => row.value !== String(this.offer!.terms[row.key] ?? ''))
      || JSON.stringify(this.chartererClauses) !== JSON.stringify(this.offer.chartererClauses ?? [])
      || JSON.stringify(this.shipownerClauses) !== JSON.stringify(this.offer.shipownerClauses ?? []));
  }
  get canSend(): boolean { return !!this.offer?.recapVersion && this.offer.stage === 'Negotiation' && !this.saving && !this.loading; }

  constructor() { this.reload(); }

  reload(): void {
    this.loading = true;
    this.error = '';
    this.message = '';
    this.api.recap(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.loadOffer(offer); this.loading = false; },
      error: () => { this.error = 'Unable to load negotiation. Please try again.'; this.loading = false; }
    });
  }

  private loadOffer(offer: CargoOffer): void {
    this.offer = offer;
    const fields: [keyof VoyageTerms, string, boolean?][] = [
      ['charterer', 'CHARTERER*'], ['quantity', 'CARGO QUANTITY*'], ['stowageFactor', 'STOWAGE FACTOR'],
      ['commodity', 'CARGO DESCRIPTION*', true], ['portOfLoading', 'PORT/S OF LOADING*'],
      ['loadingConditions', 'LOADING PORT/S CONDITIONS'], ['portOfDischarge', 'PORT/S OF DISCHARGE*'],
      ['dischargeConditions', 'DISCHARGE PORT/S CONDITIONS'], ['laycan', 'LAY/CAN*'],
      ['loadingDischargingTerms', 'LOADING / DISCHARGING TERMS*'], ['laytimeTerms', 'LAYTIME TERMS*'],
      ['charterPartyForm', 'C/P FORM*'], ['restrictions', 'RESTRICTIONS'], ['additionalRequirements', 'ADDITIONAL REQUIREMENTS', true],
      ['freight', 'FREIGHT'], ['demurrage', 'DEMURRAGE'], ['despatch', 'DESPATCH'], ['vesselName', 'VESSEL NAME'], ['remarks', 'REMARKS', true]
    ];
    this.rows = fields.map(([key, label, multiline]) => ({ key, label, multiline,
      original: String(offer.terms[key] ?? '') || '—', value: String(offer.terms[key] ?? '') }));
    this.chartererClauses = [...(offer.chartererClauses ?? [])];
    this.shipownerClauses = [...(offer.shipownerClauses ?? [])];
  }

  sendCounter(): void {
    if (!this.canSend || !this.offer?.recapVersion) return;
    const values: Record<string, string | number | undefined> = Object.fromEntries(this.rows.map(row => [row.key, row.value]));
    for (const key of ['quantity', 'freight', 'demurrage']) {
      const raw = String(values[key]).trim();
      const value = raw === '' && key !== 'quantity' ? undefined : Number(raw);
      if (value !== undefined && (!Number.isFinite(value) || value < 0 || (key === 'quantity' && value === 0))) {
        this.error = 'Enter valid numeric quantity, freight and demurrage (without thousands separators).';
        return;
      }
      values[key] = value;
    }
    const terms = { ...this.offer.terms, ...values } as VoyageTerms;
    this.saving = true;
    this.error = '';
    this.message = '';
    this.api.sendCounter(this.offer.id, terms, [...this.chartererClauses], [...this.shipownerClauses], this.offer.recapVersion)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: offer => { this.loadOffer(offer); this.saving = false; this.message = 'Counter offer saved.'; },
        error: response => { this.saving = false; this.error = response.status === 409
          ? 'The offer has changed. Reload before sending.' : 'Counter offer was not saved. Please try again.'; }
      });
  }

  addClause(side: 'charterer' | 'shipowner'): void {
    if (!this.canSend) return;
    (side === 'charterer' ? this.chartererClauses : this.shipownerClauses).push('New clause');
  }
}
