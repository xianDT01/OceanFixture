import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CargoOffer, NegotiationDraft, NegotiationEntry, VoyageTerms } from '../../core/models';
import { map, of, switchMap } from 'rxjs';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';

interface NegotiationRow { key: keyof VoyageTerms; label: string; original: string; value: string; multiline?: boolean; }

@Component({
  selector: 'of-negotiation', standalone: true, imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './negotiation.component.html', styleUrl: './negotiation.component.scss'
})
export class NegotiationComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(OceanFixtureApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  offer?: CargoOffer;
  draft?: NegotiationDraft;
  get draftIsCurrent(): boolean { return !!this.draft && this.draft.baseVersion === this.offer?.recapVersion; }
  loading = true;
  saving = false;
  error = '';
  message = '';
  rows: NegotiationRow[] = [];
  chartererClauses: string[] = [];
  shipownerClauses: string[] = [];
  readonly parties: ('Charterer' | 'Shipowner')[] = ['Charterer', 'Shipowner'];
  fieldUnit(field: keyof VoyageTerms): string {
    const terms = this.offer?.terms;
    return field === 'quantity' ? terms?.quantityUnit ?? '' : field === 'freight'
      ? [terms?.freightCurrency, terms?.freightUnit].filter(Boolean).join(' / ') : field === 'demurrage' ? terms?.demurrageUnit ?? '' : '';
  }
  get counter(): number { return this.offer?.counter ?? 0; }
  get turn(): string { return this.offer?.negotiationTurn ?? ''; }
  get history(): NegotiationEntry[] { return [...(this.offer?.negotiationHistory ?? [])].reverse(); }
  historyRows(entry: NegotiationEntry): { label: string; before: string; after: string }[] {
    return (Object.keys(entry.after.terms) as (keyof VoyageTerms)[]).map(key => ({
      label: key.replace(/([A-Z])/g, ' $1').toUpperCase(),
      before: String(entry.before.terms[key] ?? ''), after: String(entry.after.terms[key] ?? '')
    }));
  }
  get hasChanges(): boolean {
    return !!this.offer && (this.rows.some(row => row.value !== String(this.offer!.terms[row.key] ?? ''))
      || JSON.stringify(this.chartererClauses) !== JSON.stringify(this.offer.chartererClauses ?? [])
      || JSON.stringify(this.shipownerClauses) !== JSON.stringify(this.offer.shipownerClauses ?? []));
  }
  get canSend(): boolean { return !!this.offer?.recapVersion && this.offer.stage === 'Negotiation' && !this.saving && !this.loading && !!this.offer.permissions?.canSendCounter; }
  get ownParty(): 'Charterer' | 'Shipowner' | undefined {
    return this.offer?.permissions?.canConfirmAsCharterer ? 'Charterer'
      : this.offer?.permissions?.canConfirmAsShipowner ? 'Shipowner' : undefined;
  }
  get canAcceptFields(): boolean { return !!this.ownParty && !!this.offer?.recapVersion && this.offer.stage === 'Negotiation'
    && !this.loading && !this.saving && !this.hasChanges; }
  acceptance(field: keyof VoyageTerms, party: 'Charterer' | 'Shipowner') {
    return this.offer?.fieldAcceptances?.find(item => item.field === field && item.party === party);
  }
  toggleAcceptance(field: keyof VoyageTerms): void {
    if (!this.canAcceptFields || !this.offer?.recapVersion || !this.ownParty) return;
    const accepted = !this.acceptance(field, this.ownParty);
    this.saving = true; this.error = ''; this.message = '';
    this.api.setFieldAcceptance(this.offer.id, field, this.offer.recapVersion, accepted)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: offer => { this.loadOffer(offer); this.saving = false; this.message = accepted
          ? 'Condition accepted. Final Recap confirmation is still separate.' : 'Condition acceptance withdrawn. This does not withdraw a Recap confirmation.'; },
        error: response => { this.saving = false; this.error = response.status === 409
          ? 'Conditions have changed. Reload before accepting.' : 'Condition acceptance was not saved. Please try again.'; }
      });
  }
  canEditClauses(side: 'charterer' | 'shipowner'): boolean { return this.canSend && !!(side === 'charterer'
    ? this.offer?.permissions?.canEditChartererClauses : this.offer?.permissions?.canEditShipownerClauses); }

  constructor() { this.reload(); }

  reload(): void {
    if (this.saving) return;
    this.loading = true;
    this.error = '';
    this.message = '';
    this.api.recap(this.id).pipe(switchMap(offer => (offer.permissions?.canEditChartererClauses || offer.permissions?.canEditShipownerClauses
      ? this.api.negotiationDraft(offer.id) : of(null)).pipe(map(draft => ({ offer, draft })))), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ offer, draft }) => { this.loadOffer(offer); this.draft = draft ?? undefined; this.loading = false; },
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
        next: offer => { this.loadOffer(offer); this.draft = undefined; this.saving = false; this.message = 'Counter offer saved.'; },
        error: response => { this.saving = false; this.error = response.status === 409
          ? 'The offer or negotiation turn has changed. Reload before sending.' : response.status === 403
          ? 'You cannot send on this turn. Reload to check the current negotiation.' : 'Counter offer was not saved. Please try again.'; }
      });
  }

  addClause(side: 'charterer' | 'shipowner'): void {
    if (!this.canEditClauses(side)) return;
    (side === 'charterer' ? this.chartererClauses : this.shipownerClauses).push('New clause');
  }

  saveDraft(): void {
    if (!this.canSend || !this.offer?.recapVersion) return;
    const values = Object.fromEntries(this.rows.map(row => [row.key, row.value]));
    const ownClauses = this.offer.permissions?.canEditChartererClauses ? this.chartererClauses : this.shipownerClauses;
    this.saving = true; this.error = ''; this.message = '';
    this.api.saveNegotiationDraft(this.offer.id, this.offer.recapVersion, values, [...ownClauses])
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: draft => { this.draft = draft; this.saving = false; this.message = 'Private draft saved. Nothing has been sent.'; },
        error: response => { this.saving = false; this.error = response.status === 409 || response.status === 403
          ? 'The negotiation or turn has changed. Draft was not saved; your edits remain here.' : 'Draft was not saved. Your edits remain here.'; }
      });
  }

  restoreDraft(): void {
    if (!this.canSend || !this.draftIsCurrent || !this.draft) return;
    for (const row of this.rows) if (Object.prototype.hasOwnProperty.call(this.draft.values, row.key)) row.value = this.draft.values[row.key];
    if (this.draft.party === 'Charterer' && this.offer?.permissions?.canEditChartererClauses) this.chartererClauses = [...this.draft.ownClauses];
    if (this.draft.party === 'Shipowner' && this.offer?.permissions?.canEditShipownerClauses) this.shipownerClauses = [...this.draft.ownClauses];
    this.message = 'Private draft restored. Review before sending.';
  }

  deleteDraft(): void {
    if (!this.draft || this.saving || this.loading || !this.offer) return;
    this.saving = true; this.error = ''; this.message = '';
    this.api.deleteNegotiationDraft(this.offer.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.draft = undefined; this.saving = false; this.message = 'Saved draft deleted. Current edits have not been changed.'; },
      error: () => { this.saving = false; this.error = 'Unable to delete saved draft.'; }
    });
  }
}
