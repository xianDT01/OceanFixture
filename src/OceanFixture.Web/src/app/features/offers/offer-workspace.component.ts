import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';
import { CargoOffer, Company, CreateCargoOfferRequest, VoyageTerms } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';

@Component({ selector: 'of-offer-workspace', standalone: true, imports: [FormsModule, RouterLink],
  templateUrl: './offer-workspace.component.html', styleUrl: './offer-workspace.component.scss' })
export class OfferWorkspaceComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(OceanFixtureApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly isNew = this.route.snapshot.paramMap.get('id') === null;
  step = 1;
  owners: Company[] = [];
  loading = true;
  saving = false;
  error = '';
  offer?: CargoOffer;
  readonly selectedOwners = new Set<number>();
  draft: CreateCargoOfferRequest = {
    customerReference: '', invitedShipownerIds: [], terms: {
      charterer: 'Charterer One', quantity: 0, quantityUnit: 'MT', stowageFactor: 'NA', commodity: '',
      portOfLoading: '', loadingConditions: '1GSB AAAA', portOfDischarge: '', dischargeConditions: '1GSB AAAA',
      laycan: '', loadingDischargingTerms: 'FIOS', laytimeTerms: '72H WWD', charterPartyForm: 'GENCON 94',
      restrictions: 'MAX 24 YEARS VESSEL', additionalRequirements: '', freightCurrency: 'USD', freightUnit: 'LPSM',
      demurrageUnit: 'PDPR', despatch: '', vesselName: '', remarks: ''
    }
  };

  constructor() {
    if (this.isNew) this.loadOwners();
    else this.route.paramMap.pipe(switchMap(params => {
      this.loading = true;
      this.offer = undefined;
      this.error = '';
      return this.api.recap(params.get('id') ?? '').pipe(catchError(() => {
        this.error = 'Unable to load this cargo order. Please try again.';
        return of(undefined);
      }));
    }), takeUntilDestroyed(this.destroyRef)).subscribe(offer => { this.offer = offer; this.loading = false; });
  }

  loadOwners(): void {
    if (this.saving) return;
    this.loading = true;
    this.error = '';
    this.api.shipowners().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: companies => {
        this.owners = companies.filter(company => company.type === 'Shipowner' && company.approved);
        for (const id of this.selectedOwners) if (!this.owners.some(owner => owner.id === id)) this.selectedOwners.delete(id);
        this.loading = false;
      },
      error: () => { this.loading = false; this.owners = []; this.error = 'Unable to load shipowners. Please retry.'; }
    });
  }

  private validateTerms(): boolean {
    const terms = this.draft.terms;
    if (![this.draft.customerReference, terms.charterer, terms.commodity, terms.portOfLoading,
      terms.portOfDischarge, terms.laycan].every(value => value.trim().length > 0)
      || !Number.isFinite(terms.quantity) || terms.quantity <= 0
      || (terms.freight != null && (!Number.isFinite(terms.freight) || terms.freight < 0))) {
      this.error = 'Complete the reference, charterer, cargo, positive quantity, ports and laycan. Freight cannot be negative.';
      return false;
    }
    return true;
  }

  setStep(step: number): void {
    if (this.saving || this.offer || step < 1 || step > 3) return;
    this.error = '';
    if (step > 1 && !this.validateTerms()) return;
    if (step === 3 && (this.loading || !this.selectedOwners.size)) {
      this.error = 'Select at least one approved shipowner.';
      return;
    }
    this.step = step;
  }

  toggleOwner(id: number, checked: boolean): void {
    if (this.saving || !this.owners.some(owner => owner.id === id)) return;
    checked ? this.selectedOwners.add(id) : this.selectedOwners.delete(id);
  }

  createOffer(): void {
    if (this.saving || this.offer || this.loading || this.step !== 3) return;
    this.error = '';
    if (!this.validateTerms()) { this.step = 1; return; }
    if (!this.selectedOwners.size || [...this.selectedOwners].some(id => !this.owners.some(owner => owner.id === id))) {
      this.error = 'Select at least one approved shipowner.'; this.step = 2; return;
    }
    this.saving = true;
    const request: CreateCargoOfferRequest = { customerReference: this.draft.customerReference.trim(),
      terms: { ...this.draft.terms, freight: this.draft.terms.freight ?? undefined }, invitedShipownerIds: [...this.selectedOwners] };
    this.api.createOffer(request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: offer => { this.offer = offer; this.saving = false; void this.router.navigate(['/offers', offer.id]); },
      error: () => { this.saving = false; this.error = 'Cargo order was not saved. Check the data and try again.'; }
    });
  }

  get confirmationRows(): { label: string; value: string }[] {
    const terms = this.draft.terms;
    const fields: [string, keyof VoyageTerms][] = [
      ['CHARTERER', 'charterer'], ['CARGO DESCRIPTION', 'commodity'], ['PORT/S OF LOADING', 'portOfLoading'],
      ['LOADING CONDITIONS', 'loadingConditions'], ['PORT/S OF DISCHARGE', 'portOfDischarge'],
      ['DISCHARGE CONDITIONS', 'dischargeConditions'], ['LAY/CAN', 'laycan'], ['LOADING / DISCHARGING TERMS', 'loadingDischargingTerms'],
      ['LAYTIME TERMS', 'laytimeTerms'], ['C/P FORM', 'charterPartyForm'], ['RESTRICTIONS', 'restrictions'], ['ADDITIONAL REQUIREMENTS', 'additionalRequirements']
    ];
    return [{ label: 'MY REFERENCE', value: this.draft.customerReference },
      { label: 'CARGO QUANTITY', value: `${terms.quantity} ${terms.quantityUnit} / ${terms.stowageFactor}` },
      ...fields.map(([label, key]) => ({ label, value: String(terms[key] ?? '') || '—' })),
      { label: 'FREIGHT', value: terms.freight == null ? '—' : `${terms.freightCurrency} ${terms.freight} ${terms.freightUnit}` }];
  }

  initials(name: string): string { return name.split(' ').filter(Boolean).map(part => part[0]).join('').slice(0, 2); }
}
