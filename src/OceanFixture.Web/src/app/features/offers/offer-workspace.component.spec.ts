import { convertToParamMap, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { OfferWorkspaceComponent } from './offer-workspace.component';

describe('OfferWorkspaceComponent', () => {
  beforeEach(async () => {
    const params = convertToParamMap({});
    await TestBed.configureTestingModule({
      imports: [OfferWorkspaceComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: params }, paramMap: of(params) } },
        { provide: OceanFixtureApiService, useValue: { offer: () => of(undefined) } }
      ]
    }).compileComponents();
  });

  it('starts a new offer on the first wizard step', () => {
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    expect(component.isNew).toBeTrue();
    expect(component.step).toBe(1);
  });

  it('moves between steps and tracks selected shipowners', () => {
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    component.setStep(2);
    component.toggleOwner(119, true);
    expect(component.step).toBe(2);
    expect(component.selectedOwners.has(119)).toBeTrue();
    component.toggleOwner(119, false);
    expect(component.selectedOwners.has(119)).toBeFalse();
  });

  it('builds two-letter company initials', () => {
    const component = TestBed.createComponent(OfferWorkspaceComponent).componentInstance;
    expect(component.initials('Northstar Shipping')).toBe('NS');
  });
});
