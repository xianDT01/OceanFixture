import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { ComparativeComponent } from './comparative.component';

describe('ComparativeComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ComparativeComponent],
      providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '2941' }) } } }]
    }).compileComponents();
  });

  it('loads the route reference and the complete comparison matrix', () => {
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    expect(component.id).toBe('2941');
    expect(component.rows.length).toBe(19);
    expect(component.rows.filter(row => row.deadline).length).toBe(1);
  });

  it('contains the three carrier freight offers', () => {
    const component = TestBed.createComponent(ComparativeComponent).componentInstance;
    const freight = component.rows.find(row => row.label === 'FREIGHT');
    expect(freight?.beta).toContain('195,000');
    expect(freight?.gamma).toContain('265,000');
    expect(freight?.alfa).toContain('220,000');
  });
});
