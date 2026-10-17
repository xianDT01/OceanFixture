export type OfferStage = 'CargoOrder' | 'Negotiation' | 'Operative' | 'Archive';

export interface VoyageTerms {
  charterer: string; quantity: number; quantityUnit: string; stowageFactor: string; commodity: string;
  portOfLoading: string; loadingConditions: string; portOfDischarge: string; dischargeConditions: string;
  laycan: string; loadingDischargingTerms: string; laytimeTerms: string; charterPartyForm: string;
  restrictions: string; additionalRequirements: string; freight?: number; freightCurrency: string;
  freightUnit: string; demurrage?: number; demurrageUnit: string; despatch: string; vesselName: string; remarks: string;
}

export interface CargoOffer {
  chartererCompanyId?: number; permissions?: OfferPermissions;
  id: string; reference: number; customerReference: string; createdOn: string; stage: OfferStage;
  terms: VoyageTerms; bids: ShipownerBid[]; selectedShipownerId?: number; counter: number; confirmations: RecapConfirmation[];
  recapVersion?: string; chartererClauses?: string[]; shipownerClauses?: string[]; negotiationTurn?: 'Charterer' | 'Shipowner';
  negotiationHistory?: NegotiationEntry[];
  fieldAcceptances?: FieldAcceptance[];
}

export interface FieldAcceptance { field: string; party: 'Charterer' | 'Shipowner'; acceptedBy: string; acceptedAt: string; valueHash: string; }

export interface NegotiationSnapshot { terms: VoyageTerms; chartererClauses: string[]; shipownerClauses: string[]; }
export interface NegotiationDraft { userId: string; party: string; baseVersion: string; savedAt: string;
  values: Record<string, string>; ownClauses: string[]; }
export interface NegotiationEntry { id: string; counter: number; party: string; submittedBy: string;
  submittedAt: string; before: NegotiationSnapshot; after: NegotiationSnapshot; }

export interface RecapConfirmation { party: string; confirmedBy: string; confirmedAt: string; versionHash: string; }
export interface ShipownerBid { id: string; shipownerId: number; shipownerName: string; receivedAt: string; terms: VoyageTerms; }

export interface DashboardLane { key: OfferStage; label: string; offers: CargoOffer[]; }
export interface Company { id: number; name: string; type: string; country: string; vat: string; approved: boolean; }
export interface CreateCargoOfferRequest { customerReference: string; terms: VoyageTerms; invitedShipownerIds: number[]; }
export interface OfferPermissions { canSelectFirmOffer: boolean; canSendCounter: boolean; canConfirmAsCharterer: boolean;
  canConfirmAsShipowner: boolean; canEditChartererClauses: boolean; canEditShipownerClauses: boolean; canSubmitBid?: boolean; }
