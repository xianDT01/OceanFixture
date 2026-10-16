import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CargoOffer, Company } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { OfferWorkspaceComponent } from './offer-workspace.component';

describe('Cargo order creation workflow', () => {
  let api: jasmine.SpyObj<OceanFixtureApiService>;
  let navigate: jasmine.Spy;
  const owner: Company = { id: 119, name: 'Northstar Shipping', country: 'Germany', type: 'Shipowner', approved: true, vat: '' };
  beforeEach(async () => {
    api = jasmine.createSpyObj('api', ['shipowners', 'createOffer', 'recap']);
    api.shipowners.and.returnValue(of([owner, { ...owner, id: 120, approved: false }, { ...owner, id: 39, type: 'Charterer' }]));
    const params = convertToParamMap({});
    await TestBed.configureTestingModule({ imports: [OfferWorkspaceComponent], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: params }, paramMap: of(params) } },
      { provide: OceanFixtureApiService, useValue: api }] }).compileComponents();
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  function fill(component: OfferWorkspaceComponent): void {
    component.draft.customerReference = 'NEW CARGO';
    Object.assign(component.draft.terms, { quantity: 8500, commodity: 'Steel', portOfLoading: 'Vigo',
      portOfDischarge: 'Bilbao', laycan: '20/10/26 - 23/10/26', freight: 123000 });
  }

  it('loads only approved shipowners from the API', () => {
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    expect(component.isNew).toBeTrue();
    expect(component.step).toBe(1);
    expect(component.owners).toEqual([owner]);
    expect(component.initials('Northstar Shipping')).toBe('NS');
    component.toggleOwner(120, true);
    expect(component.selectedOwners.size).toBe(0);
  });

  it('binds typed reference and numeric quantity to the draft', async () => {
    const fixture = TestBed.createComponent(OfferWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const reference = fixture.nativeElement.querySelector('input[name=reference]');
    reference.value = 'TYPED REFERENCE';
    reference.dispatchEvent(new Event('input'));
    const quantity = fixture.nativeElement.querySelector('input[name=quantity]');
    quantity.value = '8500.5';
    quantity.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(fixture.componentInstance.draft.customerReference).toBe('TYPED REFERENCE');
    expect(fixture.componentInstance.draft.terms.quantity).toBe(8500.5);
  });

  it('blocks incomplete cargo and missing recipients', () => {
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    component.setStep(2);
    expect(component.step).toBe(1);
    expect(component.error).toContain('Complete');
    fill(component);
    component.setStep(2); component.setStep(3);
    expect(component.step).toBe(2);
    expect(component.error).toContain('shipowner');
  });

  it('preserves fields and checkbox selection when moving back', () => {
    const fixture = TestBed.createComponent(OfferWorkspaceComponent);
    const component = fixture.componentInstance;
    fill(component); component.setStep(2); component.toggleOwner(119, true);
    component.setStep(3); component.setStep(2);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[type=checkbox]').checked).toBeTrue();
    component.setStep(1);
    expect(component.draft.terms.commodity).toBe('Steel');
  });

  it('shows actual entered conditions in the confirmation', () => {
    const fixture = TestBed.createComponent(OfferWorkspaceComponent);
    const component = fixture.componentInstance;
    fill(component); component.toggleOwner(119, true); component.setStep(3);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('NEW CARGO');
    expect(fixture.nativeElement.textContent).toContain('USD 123000 LPSM');
    expect(fixture.nativeElement.textContent).toContain('Northstar Shipping');
  });

  it('submits current data once and navigates only after server acknowledgement', () => {
    const pending = new Subject<CargoOffer>();
    api.createOffer.and.returnValue(pending);
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    fill(component); component.toggleOwner(119, true); component.setStep(3);
    component.createOffer(); component.createOffer();
    expect(api.createOffer).toHaveBeenCalledTimes(1);
    const request = api.createOffer.calls.mostRecent().args[0];
    expect(request.customerReference).toBe('NEW CARGO');
    expect(request.terms.quantity).toBe(8500);
    expect(request.invitedShipownerIds).toEqual([119]);
    expect(navigate).not.toHaveBeenCalled();
    pending.next({ id: 'created-id', reference: 2958 } as CargoOffer);
    expect(navigate).toHaveBeenCalledOnceWith(['/offers', 'created-id']);
    component.createOffer();
    expect(api.createOffer).toHaveBeenCalledTimes(1);
  });

  it('retains draft and recipients if saving fails', () => {
    api.createOffer.and.returnValue(throwError(() => ({ status: 503 })));
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    fill(component); component.toggleOwner(119, true); component.setStep(3); component.createOffer();
    expect(component.error).toContain('not saved');
    expect(component.draft.customerReference).toBe('NEW CARGO');
    expect(component.selectedOwners.has(119)).toBeTrue();
    expect(component.saving).toBeFalse();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not invent recipients when the catalog fails', () => {
    api.shipowners.and.returnValue(throwError(() => ({ status: 503 })));
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    expect(component.owners).toEqual([]);
    expect(component.error).toContain('Unable to load');
    component.createOffer();
    expect(api.createOffer).not.toHaveBeenCalled();
  });

  it('loads an existing cargo order from the API without requesting the catalog', () => {
    const params = convertToParamMap({ id: '2958' });
    Object.assign(TestBed.inject(ActivatedRoute).snapshot, { paramMap: params });
    Object.assign(TestBed.inject(ActivatedRoute), { paramMap: of(params) });
    api.recap.and.returnValue(of({ id: 'saved-id', customerReference: 'SAVED', terms: {}, stage: 'CargoOrder' } as CargoOffer));
    const fixture = TestBed.createComponent(OfferWorkspaceComponent);
    fixture.detectChanges();
    expect(api.recap).toHaveBeenCalledWith('2958');
    expect(api.shipowners).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('SAVED');
  });
});
