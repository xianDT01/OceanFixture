import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

interface ComparisonRow { label: string; original: string; beta: string; gamma: string; alfa: string; deadline?: boolean; }

@Component({
  selector: 'of-comparative', standalone: true, imports: [RouterLink],
  templateUrl: './comparative.component.html', styleUrl: './comparative.component.scss'
})
export class ComparativeComponent {
  private readonly route = inject(ActivatedRoute);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '2941';
  readonly rows: ComparisonRow[] = [
    { label: 'CHARTERER', original: 'Charterer One', beta: 'Charterer One', gamma: 'Charterer One', alfa: 'Charterer One' },
    { label: 'CARGO QUANTITY', original: '9,000 MT / NA', beta: '9,000 MT / NA', gamma: '9,000 MT / NA', alfa: '9,000 MT / NA' },
    { label: 'CARGO DESCRIPTION', original: 'Cold-rolled steel coils, securely strapped and wrapped for maritime transport. Fully stackable / under deck only.', beta: 'Cold-rolled steel coils, securely strapped and wrapped for maritime transport. Fully stackable / under deck only.', gamma: 'Cold-rolled steel coils, securely strapped and wrapped for maritime transport. Fully stackable / under deck only.', alfa: 'Cold-rolled steel coils, securely strapped and wrapped for maritime transport. Fully stackable / under deck only.' },
    { label: 'PORT/S OF LOADING', original: 'Derince (TRDRC)', beta: 'Derince (TRDRC)', gamma: 'Izmir (TRIZM)', alfa: 'Derince (TRDRC)' },
    { label: 'LOADING CONDITIONS', original: '1GSB AAAA', beta: '1GSB AAAA', gamma: '1GSB AAAA', alfa: '1GSB AAAA' },
    { label: 'PORT/S OF DISCHARGE', original: 'Bilbao (ESBIO)', beta: 'Bilbao (ESBIO)', gamma: 'Bilbao (ESBIO)', alfa: 'Bilbao (ESBIO)' },
    { label: 'DISCHARGE CONDITIONS', original: '1GSB AAAA', beta: '1GSB AAAA', gamma: '1GSB AAAA', alfa: '1GSB AAAA' },
    { label: 'LAY/CAN', original: '18/02/25 - 20/02/25', beta: '18/02/25 - 20/02/25', gamma: '24/02/25 - 26/02/25', alfa: '16/02/25 - 18/02/25' },
    { label: 'LOADING / DISCHARGING TERMS', original: "FIOS LSD Owners' Account and Time", beta: "FIOS LSD Owners' Account and Time", gamma: 'FIOS LSD', alfa: 'FIOS LSD' },
    { label: 'LAYTIME TERMS', original: '72H WWD FSHEX / 72H WWD SSHEX', beta: '72H WWD FSHEX / 72H WWD SSHEX', gamma: '72H WWD FSHINC / 72H SSHINC', alfa: '72H WWD FSHINC / 72H WWD SSHINC' },
    { label: 'C/P FORM', original: 'GENCON 94 logically amended', beta: 'GENCON 94 logically amended', gamma: 'GENCON 94 logically amended', alfa: 'GENCON 94 logically amended' },
    { label: 'RESTRICTIONS', original: 'MAX 24 YEARS VESSEL', beta: 'MAX 24 YEARS VESSEL', gamma: 'MAX 24 YEARS VESSEL', alfa: 'MAX 24 YEARS VESSEL' },
    { label: 'ADDITIONAL REQUIREMENTS', original: 'GEARED VESSEL', beta: 'GEARED VESSEL', gamma: 'GEARED VESSEL', alfa: 'GEARED VESSEL' },
    { label: 'FREIGHT', original: '—', beta: 'EUR 195,000 LPSM', gamma: 'USD 265,000 LPSM', alfa: 'EUR 220,000 LPSM' },
    { label: 'DEMURRAGE', original: '—', beta: 'EUR 7,000 PDPR', gamma: 'USD 12,000 PDPR', alfa: 'EUR 9,500 PDPR' },
    { label: 'DESPATCH', original: '—', beta: 'HD', gamma: 'FD', alfa: 'FD' },
    { label: 'VESSEL NAME', original: '—', beta: 'EVER-SKY', gamma: 'GREENSTONE', alfa: 'EVER-OCEAN' },
    { label: 'REMARKS', original: '—', beta: 'SUB SUB SUB', gamma: '—', alfa: 'SUB DETAILS' },
    { label: 'NEW DEADLINE', original: '—', beta: '05/02/25 17:30', gamma: '04/02/25 17:00', alfa: '05/02/25 13:00', deadline: true }
  ];
}
