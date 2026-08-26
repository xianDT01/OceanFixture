import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  const lanes = [
    { key: 'CargoOrder' as const, label: 'CARGO ORDERS', offers: [] },
    { key: 'Negotiation' as const, label: 'NEGOTIATION', offers: [] }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideRouter([]), { provide: OceanFixtureApiService, useValue: { dashboard: () => of(lanes) } }]
    }).compileComponents();
  });

  it('loads dashboard lanes and calculates their offer count', async () => {
    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    expect(await firstValueFrom(component.lanes$)).toEqual(lanes);
    expect(await firstValueFrom(component.count$)).toBe(0);
  });

  it('filters values case-insensitively', () => {
    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.search = 'bilbao';
    expect(component.matches('Derince to BILBAO')).toBeTrue();
    expect(component.matches('Vigo to Aviles')).toBeFalse();
  });

  it('matches every value when search is empty', () => {
    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    expect(component.matches('anything')).toBeTrue();
  });
});
