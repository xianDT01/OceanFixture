import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { RecapComponent } from './recap.component';

describe('RecapComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [RecapComponent], providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2942' }) } } }] }).compileComponents();
  });

  it('loads all agreed terms and clauses', () => {
    const component = TestBed.createComponent(RecapComponent).componentInstance;
    expect(component.id).toBe('2942');
    expect(component.rows.length).toBe(20);
    expect(component.chartererClauses.length).toBe(6);
    expect(component.shipownerClauses.length).toBe(1);
  });

  it('is only fully confirmed after both parties confirm', () => {
    const component = TestBed.createComponent(RecapComponent).componentInstance;
    component.confirm('charterer');
    expect(component.fullyConfirmed).toBeFalse();
    component.confirm('shipowner');
    expect(component.fullyConfirmed).toBeTrue();
  });
});
