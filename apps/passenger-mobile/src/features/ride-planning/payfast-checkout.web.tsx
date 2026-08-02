import type { PayFastCheckout } from '@rydo/mobile-api-client';
import { RydoButton, colors, spacing } from '@rydo/mobile-design-system';
import { Modal, StyleSheet, Text, View } from 'react-native';

export interface PayFastCheckoutModalProps {
  checkout: PayFastCheckout | null;
  visible: boolean;
  onClose(): void;
}

export function PayFastCheckoutModal({ checkout, visible, onClose }: PayFastCheckoutModalProps) {
  if (!checkout) return null;
  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.card}>
          <Text selectable style={styles.title}>Continue to secure card payment</Text>
          <Text selectable style={styles.copy}>PayFast opens in a separate secure browser tab.</Text>
          <RydoButton label="Open PayFast" onPress={() => submitCheckout(checkout)} />
          <RydoButton label="Not now" variant="secondary" onPress={onClose} />
        </View>
      </View>
    </Modal>
  );
}

function submitCheckout(checkout: PayFastCheckout) {
  const form = document.createElement('form');
  form.method = 'post';
  form.action = checkout.processUrl;
  form.target = '_blank';
  Object.entries(checkout.fields).forEach(([name, value]) => {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = name;
    input.value = value;
    form.appendChild(input);
  });
  document.body.appendChild(form);
  form.submit();
  form.remove();
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.xl, backgroundColor: 'rgba(9, 25, 50, 0.55)' },
  card: { width: '100%', maxWidth: 420, gap: spacing.md, padding: spacing.xl, borderRadius: 28, backgroundColor: colors.white },
  title: { color: colors.navy, fontSize: 22, fontWeight: '900', textAlign: 'center' },
  copy: { color: colors.textMuted, fontSize: 14, lineHeight: 20, textAlign: 'center' },
});
