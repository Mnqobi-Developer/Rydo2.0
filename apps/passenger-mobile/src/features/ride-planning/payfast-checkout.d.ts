import type { PayFastCheckout } from '@rydo/mobile-api-client';
import type { ComponentType } from 'react';

export interface PayFastCheckoutModalProps {
  checkout: PayFastCheckout | null;
  visible: boolean;
  onClose(): void;
}

export const PayFastCheckoutModal: ComponentType<PayFastCheckoutModalProps>;
