import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CargoOffer } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { RecapComponent } from './recap.component';

describe('Recap API workflow', () => {
  let api: jasmine.SpyObj<OceanFixtureApiService>;
  let offer: CargoOffer;
  beforeEach(async () => {
    offer = { id: 'offer-id', reference: 2942, customerReference: 'FROM API', createdOn: '2025-02-03', stage: 'Negotiation',
      recapVersion: 'version-1', bids: [], counter: 2, confirmations: [], terms: { charterer: 'Customer', quantity: 4000,
        quantityUnit: 'MT', stowageFactor: 'NA', commodity: 'Steel', portOfLoading: 'La Spezia', loadingConditions: '',
        portOfDischarge: 'Vigo', dischargeConditions: '', laycan: '', loadingDischargingTerms: '', laytimeTerms: '',
        charterPartyForm: '', restrictions: '', additionalRequirements: '', freightCurrency: 'EUR', freightUnit: '',
        demurrageUnit: '', despatch: '', vesselName: '', remarks: '' } };
    api = jasmine.createSpyObj('api', ['recap', 'confirmRecap']);
    api.recap.and.returnValue(of(offer));
    await TestBed.configureTestingModule({ imports: [RecapComponent], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2942' }) } } },
      { provide: OceanFixtureApiService, useValue: api }] }).compileComponents();
  });

  it('loads API data and renders its reference', () => {
    const fixture = TestBed.createComponent(RecapComponent);
    fixture.detectChanges();
    expect(api.recap).toHaveBeenCalledWith('2942');
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('FROM API');
  });

  it('waits for server acknowledgement and prevents duplicate requests', () => {
    const pending = new Subject<CargoOffer>();
    api.confirmRecap.and.returnValue(pending);
    const component = TestBed.createComponent(RecapComponent).componentInstance;
    component.confirm('charterer'); component.confirm('charterer');
    expect(api.confirmRecap).toHaveBeenCalledOnceWith('offer-id', 'charterer', 'version-1');
    expect(component.confirmed('charterer')).toBeFalse();
    pending.next({ ...offer, confirmations: [{ party: 'charterer', confirmedBy: 'Alice', confirmedAt: '', versionHash: 'version-1' }] });
    expect(component.confirmed('charterer')).toBeTrue();
    expect(component.fullyConfirmed).toBeFalse();
  });

  it('shows conflicts without claiming successful confirmation', () => {
    api.confirmRecap.and.returnValue(throwError(() => ({ status: 409 })));
    const component = TestBed.createComponent(RecapComponent).componentInstance;
    component.confirm('shipowner');
    expect(component.error).toContain('Reload');
    expect(component.confirmed('shipowner')).toBeFalse();
    expect(component.saving).toBeFalse();
  });

  it('restores both confirmations when reloading from the server', () => {
    api.recap.and.returnValue(of({ ...offer, stage: 'Operative', confirmations: ['charterer', 'shipowner'].map(party =>
      ({ party, confirmedBy: 'User', confirmedAt: '', versionHash: 'version-1' })) }));
    expect(TestBed.createComponent(RecapComponent).componentInstance.fullyConfirmed).toBeTrue();
  });
});
