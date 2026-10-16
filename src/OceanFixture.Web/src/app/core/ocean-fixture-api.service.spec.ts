import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OceanFixtureApiService } from './ocean-fixture-api.service';
import { VoyageTerms } from './models';

describe('OceanFixtureApiService negotiation', () => {
  let api: OceanFixtureApiService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(OceanFixtureApiService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('posts complete negotiation data and its version to the API', () => {
    const terms = { quantity: 4000, freight: 120000 } as VoyageTerms;
    api.sendCounter('offer-id', terms, ['Charterer clause'], ['Owner clause'], 'version-1').subscribe();
    const request = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/negotiation/counter');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ terms, chartererClauses: ['Charterer clause'],
      shipownerClauses: ['Owner clause'], versionHash: 'version-1' });
    request.flush({});
  });

  it('posts the selected shipowner and the comparison version', () => {
    api.selectFirmOffer('offer-id', 120, 'version-1').subscribe();
    const request = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/firm-offer/120');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ versionHash: 'version-1' });
    request.flush({});
  });

  it('propagates conflicts instead of returning simulated success', () => {
    let status = 0;
    api.sendCounter('offer-id', {} as VoyageTerms, [], [], 'old').subscribe({
      next: () => fail('A conflict must not return success'), error: error => status = error.status
    });
    http.expectOne('http://localhost:5100/v1/api/offers/offer-id/negotiation/counter')
      .flush({}, { status: 409, statusText: 'Conflict' });
    expect(status).toBe(409);
  });
});
