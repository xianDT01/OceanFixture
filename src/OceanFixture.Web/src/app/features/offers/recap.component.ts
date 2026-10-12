import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { CargoOffer, VoyageTerms } from '../../core/models';

@Component({ selector: 'of-recap', standalone: true, imports: [RouterLink],
  templateUrl: './recap.component.html', styleUrl: './recap.component.scss' })
export class RecapComponent {
  private readonly api = inject(OceanFixtureApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  offer?: CargoOffer;
  loading = true;
  saving = false;
  error = '';

  constructor() { this.reload(); }

  reload(): void {
    this.loading = true;
    this.error = '';
    this.api.recap(this.route.snapshot.paramMap.get('id') ?? '').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.offer = offer; this.loading = false; },
      error: () => { this.error = 'Unable to load this recap. Please try again.'; this.loading = false; }
    });
  }

  confirmed(party: string): boolean {
    return !!this.offer?.confirmations.some(item => item.party.toLowerCase() === party && item.versionHash === this.offer?.recapVersion);
  }
  get fullyConfirmed(): boolean { return this.confirmed('charterer') && this.confirmed('shipowner'); }
  get canConfirm(): boolean { return !!this.offer?.recapVersion && ['Negotiation', 'Operative'].includes(this.offer.stage); }

  confirm(party: 'charterer' | 'shipowner'): void {
    if (!this.offer?.recapVersion || !this.canConfirm || this.saving || this.confirmed(party)) return;
    this.saving = true;
    this.error = '';
    this.api.confirmRecap(this.offer.id, party, this.offer.recapVersion).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.offer = offer; this.saving = false; },
      error: response => {
        this.error = response.status === 409 ? 'The recap has changed. Reload before confirming.' : 'Confirmation was not saved. Please try again.';
        this.saving = false;
      }
    });
  }

  get rows(): { label: string; value: string }[] {
    if (!this.offer) return [];
    const terms = this.offer.terms;
    const fields: [string, keyof VoyageTerms][] = [
      ['CHARTERER', 'charterer'], ['CARGO DESCRIPTION', 'commodity'], ['PORT/S OF LOADING', 'portOfLoading'],
      ['LOADING CONDITIONS', 'loadingConditions'], ['PORT/S OF DISCHARGE', 'portOfDischarge'],
      ['DISCHARGE CONDITIONS', 'dischargeConditions'], ['LAY/CAN', 'laycan'],
      ['LOADING / DISCHARGING TERMS', 'loadingDischargingTerms'], ['LAYTIME TERMS', 'laytimeTerms'],
      ['C/P FORM', 'charterPartyForm'], ['RESTRICTIONS', 'restrictions'], ['ADDITIONAL REQUIREMENTS', 'additionalRequirements'],
      ['FREIGHT', 'freight'], ['DEMURRAGE', 'demurrage'], ['DESPATCH', 'despatch'], ['VESSEL NAME', 'vesselName'], ['REMARKS', 'remarks']
    ];
    return [
      { label: 'MY REFERENCE', value: this.offer.customerReference },
      { label: 'CARGO QUANTITY', value: `${terms.quantity} ${terms.quantityUnit} / ${terms.stowageFactor}` },
      ...fields.map(([label, key]) => ({ label, value: String(terms[key] ?? '—') || '—' }))
    ];
  }
}
