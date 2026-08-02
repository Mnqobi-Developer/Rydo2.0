import type { PayFastCheckout } from '@rydo/mobile-api-client';
import { colors, spacing } from '@rydo/mobile-design-system';
import { lazy, Suspense } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';

const LazyWebView = lazy(() =>
  import('react-native-webview').then((module) => ({ default: module.WebView })),
);

export interface PayFastCheckoutModalProps {
  checkout: PayFastCheckout | null;
  visible: boolean;
  onClose(): void;
}

export function PayFastCheckoutModal({ checkout, visible, onClose }: PayFastCheckoutModalProps) {
  if (!checkout) return null;

  return (
    <Modal animationType="slide" onRequestClose={onClose} visible={visible}>
      <View style={styles.container}>
        <View style={styles.header}>
          <View>
            <Text selectable style={styles.title}>Secure card payment</Text>
            <Text selectable style={styles.subtitle}>Checkout is hosted and processed by PayFast.</Text>
          </View>
          <Pressable accessibilityLabel="Close PayFast checkout" hitSlop={12} onPress={onClose}>
            <Text style={styles.close}>Close</Text>
          </Pressable>
        </View>
        <Suspense fallback={<View style={styles.loading}><Text style={styles.loadingText}>Opening secure checkout…</Text></View>}>
          <LazyWebView
            originWhitelist={['https://*']}
            source={{ html: checkoutHtml(checkout) }}
            startInLoadingState
            javaScriptEnabled
          />
        </Suspense>
      </View>
    </Modal>
  );
}

function checkoutHtml(checkout: PayFastCheckout) {
  const fields = Object.entries(checkout.fields)
    .map(([name, value]) => `<input type="hidden" name="${escapeHtml(name)}" value="${escapeHtml(value)}" />`)
    .join('');
  return `<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1" /></head><body><form id="payfast" method="post" action="${escapeHtml(checkout.processUrl)}">${fields}</form><script>document.getElementById('payfast').submit();</script></body></html>`;
}

function escapeHtml(value: string) {
  return value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.white },
  header: { minHeight: 82, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md, paddingHorizontal: spacing.lg, paddingTop: spacing.md, borderBottomWidth: 1, borderBottomColor: colors.border },
  title: { color: colors.navy, fontSize: 18, fontWeight: '900' },
  subtitle: { color: colors.textMuted, marginTop: 3, fontSize: 12 },
  close: { color: colors.blue, fontSize: 15, fontWeight: '800' },
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.xl },
  loadingText: { color: colors.textMuted, fontSize: 14, fontWeight: '700' },
});
