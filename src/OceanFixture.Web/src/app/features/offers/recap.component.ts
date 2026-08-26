import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

interface RecapRow { label: string; value: string; }

@Component({
  selector: 'of-recap', standalone: true, imports: [RouterLink],
  templateUrl: './recap.component.html', styleUrl: './recap.component.scss'
})
export class RecapComponent {
  private readonly route = inject(ActivatedRoute);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '2942';
  chartererConfirmed = false;
  shipownerConfirmed = false;
  readonly rows: RecapRow[] = [
    { label: 'MY REFERENCE', value: 'ALTBIO 331-25' }, { label: 'CHARTERER', value: 'Charterer One' },
    { label: 'SHIPOWNER', value: 'Carrier Alfa' }, { label: 'CARGO QUANTITY', value: '4,000 MT / NA' },
    { label: 'CARGO DESCRIPTION', value: 'Steel plates, high-strength carbon steel, 12mm thickness, 2m x 6m dimensions.' },
    { label: 'PORT/S OF LOADING', value: 'La Spezia (ITSPE)' }, { label: 'LOADING CONDITIONS', value: '1GSB AAAA' },
    { label: 'PORT/S OF DISCHARGE', value: 'Vigo (ESVGO)' }, { label: 'DISCHARGE CONDITIONS', value: '1GSB AAAA' },
    { label: 'LAY/CAN', value: '16/02/25 - 19/02/25' }, { label: 'LOADING / DISCHARGING TERMS', value: 'FLT HH' },
    { label: 'LAYTIME TERMS', value: 'NA' }, { label: 'C/P FORM', value: 'GENCON 94 logically amended' },
    { label: 'RESTRICTIONS', value: 'MAX 24 YEARS VESSEL' }, { label: 'ADDITIONAL REQUIREMENTS', value: 'GEARED VESSEL' },
    { label: 'FREIGHT', value: 'EUR 220,000 LPSM' }, { label: 'DEMURRAGE', value: 'EUR 9,500 PDPR' },
    { label: 'DESPATCH', value: 'FD' }, { label: 'VESSEL NAME', value: 'EVER-OCEAN' }, { label: 'REMARKS', value: 'SUB DETAILS' }
  ];
  readonly chartererClauses = [
    'Vessel to be tight, staunch and in every way suitable to perform the intended voyage.',
    'Vessel to have all certificates in force according to local and international shipping regulations.',
    'Owners undertake before and at the beginning of the voyage to make the vessel seaworthy.',
    'Vessel and Owners to be ISM certified and maintained during the charter party.',
    "Vessel to comply with ISPS Code; time lost due to non-compliance for Owners' account.",
    'Vessel to be fully insured for cargo liability, hull and machinery and basic war risks.'
  ];
  readonly shipownerClauses = ['Subject to vessel approval and final stem confirmation.'];

  get fullyConfirmed(): boolean { return this.chartererConfirmed && this.shipownerConfirmed; }
  confirm(party: 'charterer' | 'shipowner'): void {
    if (party === 'charterer') this.chartererConfirmed = true;
    else this.shipownerConfirmed = true;
  }
}
