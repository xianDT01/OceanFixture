import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CargoOffer, Company, CreateCargoOfferRequest, DashboardLane, NegotiationDraft, VoyageTerms } from './models';
import { API_URL } from './session.service';

@Injectable({ providedIn: 'root' })
export class OceanFixtureApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = API_URL;
  shipowners(): Observable<Company[]> { return this.http.get<Company[]>(`${this.baseUrl}/companies?type=Shipowner`); }
  createOffer(request: CreateCargoOfferRequest): Observable<CargoOffer> { return this.http.post<CargoOffer>(`${this.baseUrl}/offers`, request); }
  dashboard(): Observable<DashboardLane[]> { return this.http.get<DashboardLane[]>(`${this.baseUrl}/offers/dashboard`); }
  recap(id: string): Observable<CargoOffer> { return this.http.get<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}`); }
  submitBid(id: string, terms: VoyageTerms, versionHash: string): Observable<CargoOffer> {
    return this.http.post<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/bids`, { terms, versionHash });
  }
  selectFirmOffer(id: string, shipownerId: number, versionHash: string): Observable<CargoOffer> {
    return this.http.post<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/firm-offer/${shipownerId}`, { versionHash });
  }
  sendCounter(id: string, terms: VoyageTerms, chartererClauses: string[], shipownerClauses: string[], versionHash: string): Observable<CargoOffer> {
    return this.http.post<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/negotiation/counter`,
      { terms, chartererClauses, shipownerClauses, versionHash });
  }
  negotiationDraft(id: string): Observable<NegotiationDraft | null> {
    return this.http.get<NegotiationDraft | null>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/negotiation/draft`);
  }
  saveNegotiationDraft(id: string, baseVersion: string, values: Record<string, string>, ownClauses: string[]): Observable<NegotiationDraft> {
    return this.http.put<NegotiationDraft>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/negotiation/draft`, { baseVersion, values, ownClauses });
  }
  deleteNegotiationDraft(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/negotiation/draft`);
  }
  confirmRecap(id: string, party: string, versionHash: string): Observable<CargoOffer> {
    return this.http.post<CargoOffer>(`${this.baseUrl}/offers/${encodeURIComponent(id)}/recap/confirm`, { party, versionHash });
  }
  offer(id: string): Observable<CargoOffer | undefined> { return this.recap(id); }
}
