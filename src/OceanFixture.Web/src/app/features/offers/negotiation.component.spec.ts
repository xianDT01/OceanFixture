import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { NegotiationComponent } from './negotiation.component';

describe('NegotiationComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NegotiationComponent],
      providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2942' }) } } }]
    }).compileComponents();
  });

  it('loads the negotiation reference and complete terms', () => {
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    expect(component.id).toBe('2942');
    expect(component.rows.length).toBe(19);
    expect(component.chartererClauses.length).toBe(6);
  });

  it('increments counters and changes the negotiating turn', () => {
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.sendCounter();
    expect(component.counter).toBe(3);
    expect(component.turn).toBe('Shipowner');
  });

  it('adds clauses to the selected party', () => {
    const component = TestBed.createComponent(NegotiationComponent).componentInstance;
    component.addClause('shipowner');
    expect(component.shipownerClauses.length).toBe(2);
    expect(component.chartererClauses.length).toBe(6);
  });
});
