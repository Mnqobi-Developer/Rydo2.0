export const operationsHubPath = '/hubs/operations';

export const operationsEvents = {
  tripUpdated: 'TripUpdated',
  tripOfferUpdated: 'TripOfferUpdated',
  driverAvailabilityUpdated: 'DriverAvailabilityUpdated',
  paymentUpdated: 'PaymentUpdated',
  disputeUpdated: 'DisputeUpdated',
  driverReviewUpdated: 'DriverReviewUpdated',
  adminOperationsChanged: 'AdminOperationsChanged',
} as const;

export interface TripRealtimeResult {
  id: string;
  passengerUserId: string;
  driverUserId: string | null;
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
  status: 'Requested' | 'Accepted' | 'DriverArrived' | 'InProgress' | 'Completed' | 'Cancelled';
  requestedAt: string;
  updatedAt: string;
  acceptedAt: string | null;
  driverArrivedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancelledByUserId: string | null;
  cancellationReason: string | null;
  finalFareAmount: number | null;
  version: number;
}

export interface TripOfferRealtimeResult {
  id: string;
  tripId: string;
  driverUserId: string;
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
  pickupDistanceKilometres: number;
  status: 'Pending' | 'Accepted' | 'Declined' | 'Expired';
  offeredAt: string;
  expiresAt: string;
  respondedAt: string | null;
  version: number;
}

export interface DriverAvailabilityRealtimeResult {
  driverUserId: string;
  isOnline: boolean;
  latitude: number;
  longitude: number;
  locationUpdatedAt: string | null;
  updatedAt: string;
  version: number;
}

export interface PaymentRealtimeResult {
  id: string;
  tripId: string;
  passengerUserId: string;
  method: 'Cash' | 'PayFast';
  status: 'AwaitingPayment' | 'Paid' | 'Cancelled' | 'Failed';
  amount: number;
  currency: string;
  providerPaymentId: string | null;
  createdAt: string;
  updatedAt: string;
  paidAt: string | null;
  failedAt: string | null;
  failureReason: string | null;
  version: number;
}

export interface DisputeMessageRealtimeResult {
  id: string;
  authorUserId: string;
  body: string;
  createdAt: string;
}

export interface DisputeRealtimeResult {
  id: string;
  tripId: string;
  openedByUserId: string;
  category: 'Fare' | 'Payment' | 'Route' | 'DriverConduct' | 'PassengerConduct' | 'Safety' | 'Other';
  subject: string;
  description: string;
  status: 'Open' | 'UnderReview' | 'Resolved' | 'Rejected';
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  resolution: string | null;
  messages: DisputeMessageRealtimeResult[];
}

export interface DriverReviewRealtimeResult {
  driverUserId: string;
  status: 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';
  rejectionReason: string | null;
  updatedAt: string;
}

export interface AdminOperationsRealtimeResult {
  resource: string;
  entityId: string;
  changeType: string;
  occurredAt: string;
}

export interface OperationsEventPayloads {
  TripUpdated: TripRealtimeResult;
  TripOfferUpdated: TripOfferRealtimeResult;
  DriverAvailabilityUpdated: DriverAvailabilityRealtimeResult;
  PaymentUpdated: PaymentRealtimeResult;
  DisputeUpdated: DisputeRealtimeResult;
  DriverReviewUpdated: DriverReviewRealtimeResult;
  AdminOperationsChanged: AdminOperationsRealtimeResult;
}

export type OperationsEventName = keyof OperationsEventPayloads;
export type OperationsEventHandlers = Partial<{
  [TEvent in OperationsEventName]: (payload: OperationsEventPayloads[TEvent]) => void;
}>;
