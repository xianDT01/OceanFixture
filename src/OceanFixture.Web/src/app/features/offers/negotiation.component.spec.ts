import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CargoOffer, NegotiationDraft, VoyageTerms } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { NegotiationComponent } from './negotiation.component';

describe('Negotiation API workflow', () => {
  let api: jasmine.SpyObj<OceanFixtureApiService>;
  let offer: CargoOffer;
  beforeEach(async () => {
    offer = { id: 'offer-id', reference: 2942, customerReference: 'API reference', createdOn: '', stage: 'Negotiation',
      recapVersion: 'v1', bids: [], counter: 2, confirmations: [], negotiationTurn: 'Charterer',
      permissions: { canSelectFirmOffer: false, canSendCounter: true, canConfirmAsCharterer: true,
        canConfirmAsShipowner: true, canEditChartererClauses: true, canEditShipownerClauses: true },
      chartererClauses: ['Existing clause'], shipownerClauses: [],
      terms: { quantity: 4000, quantityUnit: 'MT', freightCurrency: 'EUR', charterer: 'Customer', commodity: 'Steel' } as VoyageTerms };
    api = jasmine.createSpyObj('api', ['recap', 'sendCounter', 'negotiationDraft', 'saveNegotiationDraft', 'deleteNegotiationDraft']);
    api.negotiationDraft.and.returnValue(of(null));
    api.recap.and.returnValue(of(offer));
    await TestBed.configureTestingModule({ imports: [NegotiationComponent], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2942' }) } } },
      { provide: OceanFixtureApiService, useValue: api }] }).compileComponents();
  });

  it('renders server conditions and copies clauses without mutating server data', () => {
    const fixture = TestBed.createComponent(NegotiationComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h2').textContent).toBe('API reference');
    expect(fixture.componentInstance.rows.length).toBe(19);
    fixture.componentInstance.addClause('charterer');
    expect(offer.chartererClauses!.length).toBe(1);
    expect(fixture.componentInstance.hasChanges).toBeTrue();
  });

  it('sends edits and clauses, preserving units, and waits for the server', () => {
    const pending = new Subject<CargoOffer>();
    api.sendCounter.and.returnValue(pending);
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.rows.find(row => row.key === 'freight')!.value = '120000';
    component.addClause('shipowner');
    component.sendCounter(); component.sendCounter();
    expect(api.sendCounter).toHaveBeenCalledTimes(1);
    const args = api.sendCounter.calls.mostRecent().args;
    expect(args[0]).toBe('offer-id');
    expect(args[1].freight).toBe(120000);
    expect(args[1].freightCurrency).toBe('EUR');
    expect(args[3]).toEqual(['New clause']);
    expect(args[4]).toBe('v1');
    expect(component.counter).toBe(2);
    pending.next({ ...offer, terms: args[1], shipownerClauses: args[3], recapVersion: 'v2', counter: 3, negotiationTurn: 'Shipowner' });
    expect(component.counter).toBe(3);
    expect(component.turn).toBe('Shipowner');
    expect(component.hasChanges).toBeFalse();
  });

  it('rejects invalid numbers before sending', () => {
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.rows.find(row => row.key === 'quantity')!.value = '4,000';
    component.sendCounter();
    expect(api.sendCounter).not.toHaveBeenCalled();
    expect(component.error).toContain('numeric');
  });

  it('preserves edits and counter on version conflict', () => {
    api.sendCounter.and.returnValue(throwError(() => ({ status: 409 })));
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.rows[0].value = 'Edited customer';
    component.sendCounter();
    expect(component.error).toContain('Reload');
    expect(component.rows[0].value).toBe('Edited customer');
    expect(component.counter).toBe(2);
    expect(component.saving).toBeFalse();
  });

  it('does not allow adding clauses for the counterparty', () => {
    offer.permissions!.canEditShipownerClauses = false;
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.addClause('shipowner');
    expect(component.shipownerClauses).toEqual([]);
    expect(component.canEditClauses('shipowner')).toBeFalse();
  });

  it('does not allow sending when loading fails', () => {
    api.recap.and.returnValue(throwError(() => ({ status: 503 })));
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.sendCounter();
    expect(component.error).toContain('Unable to load');
    expect(api.sendCounter).not.toHaveBeenCalled();
  });

  it('disables edits and sending while waiting for the other party', async () => {
    offer.permissions!.canSendCounter = false;
    offer.negotiationTurn = 'Shipowner';
    const fixture = TestBed.createComponent(NegotiationComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.turn-notice').textContent).toContain('Shipowner');
    expect(fixture.nativeElement.querySelector('input').disabled).toBeTrue();
    fixture.componentInstance.sendCounter();
    fixture.componentInstance.addClause('charterer');
    fixture.componentInstance.saveDraft();
    expect(api.sendCounter).not.toHaveBeenCalled();
    expect(api.saveNegotiationDraft).not.toHaveBeenCalled();
    expect(fixture.componentInstance.chartererClauses).toEqual(['Existing clause']);
  });

  it('renders saved history with before and sent terms and safe text', () => {
    offer.negotiationHistory = [{ id: 'event', counter: 2, party: 'Charterer', submittedBy: 'server-user',
      submittedAt: '2026-10-17T12:00:00Z', before: { terms: { ...offer.terms, freight: 100 }, chartererClauses: [], shipownerClauses: [] },
      after: { terms: { ...offer.terms, freight: 200 }, chartererClauses: ['<b>clause</b>'], shipownerClauses: [] } }];
    const fixture = TestBed.createComponent(NegotiationComponent);
    fixture.detectChanges();
    const history = fixture.nativeElement.querySelector('.negotiation-history');
    expect(history.textContent).toContain('COUNTER OFFER HISTORY (1)');
    expect(history.textContent).toContain('server-user');
    expect(history.textContent).toContain('17/10/2026 12:00 UTC');
    expect(history.textContent).toContain('<b>clause</b>');
    expect(history.querySelector('.changed').textContent).toContain('100');
    expect(history.querySelector('.changed').textContent).toContain('200');
  });

  it('does not reload over an in-flight save', () => {
    api.sendCounter.and.returnValue(new Subject<CargoOffer>());
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.sendCounter();
    component.reload();
    expect(api.recap).toHaveBeenCalledTimes(1);
  });

  const savedDraft = (version = 'v1'): NegotiationDraft => ({ userId: 'server-user', party: 'Charterer',
    baseVersion: version, savedAt: '2026-10-17T12:00:00Z', values: { freight: 'unfinished-' }, ownClauses: ['Private'] });

  it('offers explicit restoration without automatically replacing form values', () => {
    api.negotiationDraft.and.returnValue(of(savedDraft()));
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    expect(component.rows.find(row => row.key === 'freight')!.value).toBe('');
    component.restoreDraft();
    expect(component.rows.find(row => row.key === 'freight')!.value).toBe('unfinished-');
    expect(component.chartererClauses).toEqual(['Private']);
    expect(component.shipownerClauses).toEqual([]);
    expect(offer.chartererClauses).toEqual(['Existing clause']);
    expect(api.sendCounter).not.toHaveBeenCalled();
  });

  it('saves incomplete fields without sending or changing the counter', () => {
    api.saveNegotiationDraft.and.returnValue(of(savedDraft()));
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.rows.find(row => row.key === 'freight')!.value = 'unfinished-';
    component.saveDraft();
    const args = api.saveNegotiationDraft.calls.mostRecent().args;
    expect(args[1]).toBe('v1');
    expect(args[2]['freight']).toBe('unfinished-');
    expect(args[3]).toEqual(['Existing clause']);
    expect(component.message).toContain('Nothing has been sent');
    expect(component.counter).toBe(2);
    expect(api.sendCounter).not.toHaveBeenCalled();
  });

  it('refuses restoring a stale draft', () => {
    api.negotiationDraft.and.returnValue(of(savedDraft('old')));
    const fixture = TestBed.createComponent(NegotiationComponent);
    fixture.detectChanges();
    fixture.componentInstance.restoreDraft();
    expect(fixture.componentInstance.draftIsCurrent).toBeFalse();
    expect(fixture.componentInstance.rows.find(row => row.key === 'freight')!.value).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Outdated');
  });

  it('keeps edits and previous draft when saving fails', () => {
    api.negotiationDraft.and.returnValue(of(savedDraft()));
    api.saveNegotiationDraft.and.returnValue(throwError(() => ({ status: 409 })));
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.rows[0].value = 'Edited';
    component.saveDraft();
    expect(component.rows[0].value).toBe('Edited');
    expect(component.draft?.values['freight']).toBe('unfinished-');
    expect(component.error).toContain('not saved');
    expect(component.saving).toBeFalse();
  });

  it('deletes the saved draft without changing current edits', () => {
    api.negotiationDraft.and.returnValue(of(savedDraft()));
    api.deleteNegotiationDraft.and.returnValue(of(undefined));
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.rows[0].value = 'Edited';
    component.deleteDraft();
    expect(component.draft).toBeUndefined();
    expect(component.rows[0].value).toBe('Edited');
  });

  it('does not request private drafts for the administrator', () => {
    offer.permissions!.canEditChartererClauses = false;
    offer.permissions!.canEditShipownerClauses = false;
    TestBed.createComponent(NegotiationComponent);
    expect(api.negotiationDraft).not.toHaveBeenCalled();
  });
});
