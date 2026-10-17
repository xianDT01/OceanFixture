import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CargoOffer } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { ShipownerBidComponent } from './shipowner-bid.component';

describe('Shipowner bid workflow', () => {
  let api: jasmine.SpyObj<OceanFixtureApiService>;
  let offer: CargoOffer;
  beforeEach(async () => {
    offer = { id: 'offer-id', reference: 2943, customerReference: 'INVITED CARGO', createdOn: '', stage: 'CargoOrder',
      recapVersion: 'v1', bids: [], counter: 0, confirmations: [], permissions: { canSubmitBid: true, canSelectFirmOffer: false,
        canSendCounter: false, canConfirmAsCharterer: false, canConfirmAsShipowner: false, canEditChartererClauses: false, canEditShipownerClauses: false },
      terms: { charterer: 'Customer', quantity: 8500, quantityUnit: 'MT', stowageFactor: 'NA', commodity: 'Steel',
        portOfLoading: 'Vigo', loadingConditions: '', portOfDischarge: 'Bilbao', dischargeConditions: '', laycan: '20/10/26 - 23/10/26',
        loadingDischargingTerms: 'FIOS', laytimeTerms: '', charterPartyForm: 'GENCON 94', restrictions: '', additionalRequirements: '',
        freightCurrency: 'USD', freightUnit: 'LPSM', demurrageUnit: 'PDPR', despatch: '', vesselName: '', remarks: '' } };
    api = jasmine.createSpyObj('api', ['recap', 'submitBid']); api.recap.and.returnValue(of(offer));
    await TestBed.configureTestingModule({ imports: [ShipownerBidComponent], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2943' }) } } },
      { provide: OceanFixtureApiService, useValue: api }] }).compileComponents();
  });

  it('loads the invitation and displays original conditions', () => {
    const fixture = TestBed.createComponent(ShipownerBidComponent); fixture.detectChanges();
    expect(api.recap).toHaveBeenCalledWith('2943');
    expect(fixture.nativeElement.querySelector('h2').textContent).toBe('INVITED CARGO');
    expect(fixture.componentInstance.rows.find(row => row.key === 'commodity')!.value).toBe('Steel');
    expect(fixture.componentInstance.canSubmit).toBeTrue();
  });

  it('prefills the active own offer instead of overwriting it with cargo terms', () => {
    offer.bids = [{ id: 'bid', shipownerId: 119, shipownerName: 'Owner', receivedAt: '', terms: { ...offer.terms, freight: 195000, freightCurrency: 'EUR' } }];
    const component = TestBed.createComponent(ShipownerBidComponent).componentInstance;
    expect(component.rows.find(row => row.key === 'freight')!.value).toBe('195000');
    expect(component.rows.find(row => row.key === 'freightCurrency')!.value).toBe('EUR');
    expect(component.rows.find(row => row.key === 'freight')!.original).toBe('—');
  });

  it('submits typed fields once and waits for the server', async () => {
    const pending = new Subject<CargoOffer>(); api.submitBid.and.returnValue(pending);
    const fixture = TestBed.createComponent(ShipownerBidComponent); fixture.detectChanges(); await fixture.whenStable();
    const input = fixture.nativeElement.querySelector('#bid-freight');
    input.value = '123000'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
    fixture.componentInstance.submit(); fixture.componentInstance.submit();
    expect(api.submitBid).toHaveBeenCalledTimes(1);
    const args = api.submitBid.calls.mostRecent().args;
    expect(args[0]).toBe('offer-id'); expect(args[1].freight).toBe(123000); expect(args[2]).toBe('v1');
    expect(offer.bids).toEqual([]);
    pending.next({ ...offer, recapVersion: 'v2', bids: [{ id: 'bid', shipownerId: 119, shipownerName: 'Owner', receivedAt: '', terms: args[1] }] });
    expect(fixture.componentInstance.message).toContain('saved');
    expect(fixture.componentInstance.offer!.bids.length).toBe(1);
  });

  it('blocks invalid freight before sending', () => {
    const component = TestBed.createComponent(ShipownerBidComponent).componentInstance;
    component.rows.find(row => row.key === 'freight')!.value = '-1'; component.submit();
    expect(api.submitBid).not.toHaveBeenCalled(); expect(component.error).toContain('positive');
  });

  it('preserves edits on a version conflict without announcing success', () => {
    api.submitBid.and.returnValue(throwError(() => ({ status: 409 })));
    const component = TestBed.createComponent(ShipownerBidComponent).componentInstance;
    component.rows.find(row => row.key === 'freight')!.value = '123000'; component.submit();
    expect(component.error).toContain('reload'); expect(component.message).toBe('');
    expect(component.rows.find(row => row.key === 'freight')!.value).toBe('123000'); expect(component.saving).toBeFalse();
  });

  it('does not allow submission without the server permission', () => {
    offer.permissions!.canSubmitBid = false;
    const component = TestBed.createComponent(ShipownerBidComponent).componentInstance; component.submit();
    expect(api.submitBid).not.toHaveBeenCalled(); expect(component.canSubmit).toBeFalse();
  });

  it('does not allow initial bid updates after selection', () => {
    offer.stage = 'Negotiation';
    const component = TestBed.createComponent(ShipownerBidComponent).componentInstance; component.submit();
    expect(api.submitBid).not.toHaveBeenCalled();
  });

  it('does not invent a cargo or bid when loading fails', () => {
    api.recap.and.returnValue(throwError(() => ({ status: 404 })));
    const component = TestBed.createComponent(ShipownerBidComponent).componentInstance;
    expect(component.offer).toBeUndefined(); expect(component.error).toContain('Unable'); expect(component.canSubmit).toBeFalse();
  });
});
