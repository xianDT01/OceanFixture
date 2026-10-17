import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CargoOffer, VoyageTerms } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { ComparativeComponent } from './comparative.component';

describe('Comparative API workflow', () => {
  let api: jasmine.SpyObj<OceanFixtureApiService>;
  let offer: CargoOffer;
  let navigate: jasmine.Spy;
  beforeEach(async () => {
    const terms = { charterer: 'Customer', quantity: 9000, quantityUnit: 'MT', commodity: 'Steel',
      freightCurrency: 'EUR', freightUnit: 'LPSM', demurrageUnit: 'PDPR' } as VoyageTerms;
    offer = { id: 'offer-id', reference: 2941, customerReference: 'Comparison', createdOn: '2025-02-03', stage: 'CargoOrder',
      recapVersion: 'v1', terms, counter: 0, confirmations: [], permissions: { canSelectFirmOffer: true, canSendCounter: false,
        canConfirmAsCharterer: false, canConfirmAsShipowner: false, canEditChartererClauses: false, canEditShipownerClauses: false }, bids: [
        { id: 'bid-1', shipownerId: 120, shipownerName: 'Owner One', receivedAt: '2025-02-03T13:20:00Z', terms: { ...terms, freight: 195000 } },
        { id: 'bid-2', shipownerId: 121, shipownerName: 'Owner Two', receivedAt: '2025-02-03T13:24:00Z', terms: { ...terms, freight: 265000, freightCurrency: 'USD' } }
      ] };
    api = jasmine.createSpyObj('api', ['recap', 'selectFirmOffer']);
    api.recap.and.returnValue(of(offer));
    await TestBed.configureTestingModule({ imports: [ComparativeComponent], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2941' }) } } },
      { provide: OceanFixtureApiService, useValue: api }] }).compileComponents();
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('renders a column per server bid with its currency and units', () => {
    const fixture = TestBed.createComponent(ComparativeComponent);
    fixture.detectChanges();
    expect(api.recap).toHaveBeenCalledWith('2941');
    expect(fixture.nativeElement.querySelectorAll('thead th').length).toBe(5);
    expect(fixture.nativeElement.textContent).toContain('Owner Two');
    const freight = fixture.componentInstance.rows.find(row => row.label === 'FREIGHT')!;
    expect(freight.values).toEqual(['EUR 195,000 LPSM', 'USD 265,000 LPSM']);
    expect(fixture.nativeElement.querySelectorAll('.changed-value').length).toBeGreaterThan(0);
  });

  it('saves the chosen shipowner once and navigates only after server acknowledgement', () => {
    const pending = new Subject<CargoOffer>();
    api.selectFirmOffer.and.returnValue(pending);
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    component.selectFirmOffer(offer.bids[1]); component.selectFirmOffer(offer.bids[0]);
    expect(api.selectFirmOffer).toHaveBeenCalledOnceWith('offer-id', 121, 'v1');
    expect(navigate).not.toHaveBeenCalled();
    pending.next({ ...offer, stage: 'Negotiation', selectedShipownerId: 121, recapVersion: 'v2', terms: offer.bids[1].terms });
    expect(navigate).toHaveBeenCalledOnceWith(['/offers', 'offer-id', 'negotiation']);
    expect(component.offer!.terms.freight).toBe(265000);
    expect(component.canSelect).toBeFalse();
  });

  it('stays on comparison when a version conflict occurs', () => {
    api.selectFirmOffer.and.returnValue(throwError(() => ({ status: 409 })));
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    component.selectFirmOffer(offer.bids[0]);
    expect(component.error).toContain('Reload');
    expect(navigate).not.toHaveBeenCalled();
    expect(component.saving).toBeFalse();
    expect(component.offer!.stage).toBe('CargoOrder');
  });

  it('does not select offers for operative or archived operations', () => {
    api.recap.and.returnValue(of({ ...offer, stage: 'Operative' }));
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    component.selectFirmOffer(offer.bids[0]);
    expect(api.selectFirmOffer).not.toHaveBeenCalled();
    expect(component.canSelect).toBeFalse();
  });

  it('does not let a shipowner select a firm offer when permission is absent', () => {
    offer.permissions!.canSelectFirmOffer = false;
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    component.selectFirmOffer(offer.bids[0]);
    expect(api.selectFirmOffer).not.toHaveBeenCalled();
  });

  it('shows empty bids without invented carriers', () => {
    api.recap.and.returnValue(of({ ...offer, bids: [] }));
    const fixture = TestBed.createComponent(ComparativeComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No shipowner offers received yet');
    expect(fixture.nativeElement.querySelectorAll('.firm-offer').length).toBe(0);
  });

  it('shows loading errors without local fallback bids', () => {
    api.recap.and.returnValue(throwError(() => ({ status: 503 })));
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    expect(component.error).toContain('Unable to load');
    expect(component.bids).toEqual([]);
    expect(component.canSelect).toBeFalse();
  });
});
