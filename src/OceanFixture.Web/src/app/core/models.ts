export type OfferStage = 'CargoOrder' | 'Negotiation' | 'Operative' | 'Archive';

export interface VoyageTerms {
  charterer: string; quantity: number; quantityUnit: string; stowageFactor: string; commodity: string;
  portOfLoading: string; loadingConditions: string; portOfDischarge: string; dischargeConditions: string;
  laycan: string; loadingDischargingTerms: string; laytimeTerms: string; charterPartyForm: string;
  restrictions: string; additionalRequirements: string; freight?: number; freightCurrency: string;
  freightUnit: string; demurrage?: number; demurrageUnit: string; despatch: string; vesselName: string; remarks: string;
}

export interface CargoOffer {
  id: string; reference: number; customerReference: string; createdOn: string; stage: OfferStage;
  terms: VoyageTerms; bids: unknown[]; selectedShipownerId?: number; counter: number; confirmations: RecapConfirmation[];
  recapVersion?: string; chartererClauses?: string[]; shipownerClauses?: string[]; negotiationTurn?: 'Charterer' | 'Shipowner';
}

export interface RecapConfirmation { party: string; confirmedBy: string; confirmedAt: string; versionHash: string; }

export interface DashboardLane { key: OfferStage; label: string; offers: CargoOffer[]; }
