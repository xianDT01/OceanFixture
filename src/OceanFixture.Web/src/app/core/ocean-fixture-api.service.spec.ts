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

  it('loads shipowners from the company catalog', () => {
    api.shipowners().subscribe(companies => expect(companies).toEqual([]));
    const request = http.expectOne('http://localhost:5100/v1/api/companies?type=Shipowner');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('posts cargo data and recipients without client-generated reference numbers', () => {
    const request = { customerReference: 'NEW', terms: { quantity: 8500 } as VoyageTerms, invitedShipownerIds: [119] };
    api.createOffer(request).subscribe();
    const pending = http.expectOne('http://localhost:5100/v1/api/offers');
    expect(pending.request.method).toBe('POST');
    expect(pending.request.body).toEqual(request);
    pending.flush({});
  });

  it('posts bid terms and version without a client-controlled shipowner identity', () => {
    const terms = { freight: 200000, freightCurrency: 'EUR' } as VoyageTerms;
    api.submitBid('offer-id', terms, 'version-1').subscribe();
    const request = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/bids');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ terms, versionHash: 'version-1' });
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

  it('saves private draft without a client-controlled author or party', () => {
    api.saveNegotiationDraft('offer-id', 'v1', { freight: 'unfinished-' }, ['Private']).subscribe();
    const request = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/negotiation/draft');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ baseVersion: 'v1', values: { freight: 'unfinished-' }, ownClauses: ['Private'] });
    request.flush({});
  });

  it('reads and deletes only the session users draft', () => {
    api.negotiationDraft('offer-id').subscribe(value => expect(value).toBeNull());
    const read = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/negotiation/draft');
    expect(read.request.method).toBe('GET');
    read.flush(null);
    api.deleteNegotiationDraft('offer-id').subscribe();
    const remove = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/negotiation/draft');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });

  it('sets field acceptance without allowing a client-controlled party or author', () => {
    api.setFieldAcceptance('offer-id', 'laycan', 'v1', true).subscribe();
    const request = http.expectOne('http://localhost:5100/v1/api/offers/offer-id/negotiation/fields/laycan/acceptance');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ versionHash: 'v1', accepted: true });
    request.flush({});
  });
});
