import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { CargoOffer, DashboardLane, VoyageTerms } from './models';

const terms = (quantity: number, commodity: string, pol: string, pod: string, laycan: string): VoyageTerms => ({
  charterer: 'Charterer One', quantity, quantityUnit: 'MT', stowageFactor: 'NA', commodity,
  portOfLoading: pol, loadingConditions: '1GSB AAAA', portOfDischarge: pod, dischargeConditions: '1GSB AAAA',
  laycan, loadingDischargingTerms: 'FIOS', laytimeTerms: '72H WWD', charterPartyForm: 'GENCON 94',
  restrictions: 'MAX 24 YEARS VESSEL', additionalRequirements: '', freightCurrency: 'USD', freightUnit: 'LPSM',
  demurrageUnit: 'PDPR', despatch: '', vesselName: '', remarks: ''
});

const offer = (id: string, reference: number, customerReference: string, stage: CargoOffer['stage'], voyage: VoyageTerms): CargoOffer => ({
  id, reference, customerReference, stage, terms: voyage, createdOn: '2025-02-03', bids: [], counter: 0, confirmations: []
});

const FALLBACK: DashboardLane[] = [
  { key: 'CargoOrder', label: 'CARGO ORDERS', offers: [
    offer('2957', 2957, 'AUTOMATED TEST CARGO', 'CargoOrder', terms(5000, '5,000 MT STEEL COILS', '', '', '')),
    offer('2943', 2943, 'ALT-BIO25-25', 'CargoOrder', terms(8500, 'IRON ORE AS PER ATTACHED DOCUMENT', 'Cartagena (COCTG)', 'Gijon (ESGIJ)', '06/07/25 - 10/07/25')),
    offer('2940', 2940, 'ALTBIO 441-25', 'CargoOrder', terms(12000, 'Seamless steel pipes, 12 meters', 'Tianjin Xingang (CNTXG)', 'Bejaia (DZBJA)', '23/02/25 - 27/02/25'))
  ]},
  { key: 'Negotiation', label: 'NEGOTIATION', offers: [offer('2942', 2942, 'ALTBIO 331-25', 'Negotiation', terms(4000, 'High-strength steel plates', 'La Spezia (ITSPE)', 'Vigo (ESVGO)', '16/02/25 - 19/02/25'))]},
  { key: 'Operative', label: 'OPERATIVE', offers: [
    offer('2941', 2941, 'ALT BIO 445-25', 'Operative', terms(9000, 'Cold-rolled steel coils, fully stackable', 'Derince (TRDRC)', 'Bilbao (ESBIO)', '18/02/25 - 20/02/25')),
    offer('2939', 2939, 'ALTBIO - 225-25', 'Operative', terms(21000, 'STEEL PLATES AS PER PACKING LIST', 'Gdansk (PLGDN)', 'Casablanca (MACAS)', '10/02/25 - 14/02/25'))
  ]},
  { key: 'Archive', label: 'ARCHIVE', offers: [
    offer('2937', 2937, 'ALTBIO-176-25', 'Archive', terms(7500, 'Bulk steam coal', 'Cartagena (COCTG)', 'Aviles (ESAVS)', '02/02/25 - 06/02/25')),
    offer('2938', 2938, 'ALTBIO-323-25', 'Archive', terms(6800, 'WOODEN BOXES AS PER ATTACHED', 'Vigo (ESVGO)', 'Immingham (GBIMM)', '01/02/25 - 12/02/25'))
  ]}
];

@Injectable({ providedIn: 'root' })
export class OceanFixtureApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5100/v1/api';
  dashboard(): Observable<DashboardLane[]> { return this.http.get<DashboardLane[]>(`${this.baseUrl}/offers/dashboard`).pipe(catchError(() => of(FALLBACK))); }
  recap(id: string): Observable<CargoOffer> { return this.http.get<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}`); }
  sendCounter(id: string, terms: VoyageTerms, chartererClauses: string[], shipownerClauses: string[], versionHash: string): Observable<CargoOffer> {
    return this.http.post<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/negotiation/counter`,
      { terms, chartererClauses, shipownerClauses, versionHash });
  }
  confirmRecap(id: string, party: string, versionHash: string): Observable<CargoOffer> {
    return this.http.post<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/recap/confirm`,
      { party, confirmedBy: 'OceanFixture demo user', versionHash });
  }
  offer(id: string): Observable<CargoOffer | undefined> {
    const fallback = FALLBACK.flatMap(lane => lane.offers).find(item => item.id === id || item.reference.toString() === id);
    return this.http.get<CargoOffer>(`${this.baseUrl}/offers/${id}`).pipe(catchError(() => of(fallback)));
  }
}
