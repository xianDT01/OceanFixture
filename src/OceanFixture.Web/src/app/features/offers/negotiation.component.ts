import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

interface NegotiationRow { label: string; original: string; value: string; multiline?: boolean; }

@Component({
  selector: 'of-negotiation', standalone: true, imports: [FormsModule, RouterLink],
  templateUrl: './negotiation.component.html', styleUrl: './negotiation.component.scss'
})
export class NegotiationComponent {
  private readonly route = inject(ActivatedRoute);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '2942';
  counter = 2;
  turn: 'Charterer' | 'Shipowner' = 'Charterer';
  readonly rows: NegotiationRow[] = [
    { label: 'CHARTERER*', original: 'Charterer One', value: 'Charterer One' },
    { label: 'CARGO QUANTITY*', original: '4,000 MT / NA', value: '4,000' },
    { label: 'STOWAGE FACTOR', original: 'NA', value: 'NA' },
    { label: 'CARGO DESCRIPTION*', original: 'Steel plates, high-strength carbon steel, 12mm thickness, 2m x 6m dimensions.', value: 'Steel plates, high-strength carbon steel, 12mm thickness, 2m x 6m dimensions.', multiline: true },
    { label: 'PORT/S OF LOADING*', original: 'La Spezia (ITSPE)', value: 'La Spezia (ITSPE)' },
    { label: 'LOADING PORT/S CONDITIONS', original: '1GSB AAAA', value: '1GSB AAAA' },
    { label: 'PORT/S OF DISCHARGE*', original: 'Vigo (ESVGO)', value: 'Vigo (ESVGO)' },
    { label: 'DISCHARGE PORT/S CONDITIONS', original: '1GSB AAAA', value: '1GSB AAAA' },
    { label: 'LAY/CAN*', original: '16/02/25 - 19/02/25', value: '16/02/25 - 19/02/25' },
    { label: 'LOADING / DISCHARGING TERMS*', original: 'FLT HH', value: 'FLT HH' },
    { label: 'LAYTIME TERMS*', original: 'NA', value: 'NA' },
    { label: 'C/P FORM*', original: 'GENCON 94 logically amended', value: 'GENCON 94 logically amended' },
    { label: 'RESTRICTIONS', original: 'MAX 24 YEARS VESSEL', value: 'MAX 24 YEARS VESSEL' },
    { label: 'ADDITIONAL REQUIREMENTS', original: 'GEARED VESSEL', value: 'GEARED VESSEL', multiline: true },
    { label: 'FREIGHT', original: '—', value: '' },
    { label: 'DEMURRAGE', original: '—', value: '' },
    { label: 'DESPATCH', original: '—', value: '' },
    { label: 'VESSEL NAME', original: '—', value: '' },
    { label: 'REMARKS', original: '—', value: '', multiline: true }
  ];
  chartererClauses = [
    'Vessel to be tight, staunch and in every way suitable to perform the intended voyage.',
    'Vessel to have all certificates in force according to local and international shipping regulations.',
    'Owners undertake before and at the beginning of the voyage to make the vessel seaworthy.',
    'Vessel and Owners to be ISM certified and same to be maintained during the charter party.',
    "Vessel to comply with ISPS Code; time lost due to non-compliance for Owners' account.",
    'Vessel to be fully insured for cargo liability, hull and machinery and basic war risks.'
  ];
  shipownerClauses = ['Subject to vessel approval and final stem confirmation.'];

  sendCounter(): void {
    this.counter++;
    this.turn = this.turn === 'Charterer' ? 'Shipowner' : 'Charterer';
  }
  addClause(side: 'charterer' | 'shipowner'): void {
    (side === 'charterer' ? this.chartererClauses : this.shipownerClauses).push('New clause');
  }
}
