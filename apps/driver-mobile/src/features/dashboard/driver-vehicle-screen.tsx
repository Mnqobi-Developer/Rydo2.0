import { isApiError } from '@rydo/mobile-api-client';
import { ErrorState, LoadingState, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DriverRideIcon } from '@/components/driver-ride-icon';
import { driverTheme } from '@/theme/driver-theme';

import {
  driverVehicleQuery,
  saveDriverVehicle,
  type DriverVehicle,
  type UpdateDriverVehicleInput,
} from './api';

type VehicleForm = {
  make: string;
  model: string;
  year: string;
  color: string;
  registrationNumber: string;
  vehicleIdentificationNumber: string;
  seatCapacity: string;
};

const emptyForm: VehicleForm = {
  make: '', model: '', year: '', color: '', registrationNumber: '', vehicleIdentificationNumber: '', seatCapacity: '4',
};

export function DriverVehicleScreen({ onBack, onOpenDocuments }: { onBack(): void; onOpenDocuments(): void }) {
  const vehicle = useQuery(driverVehicleQuery);

  if (vehicle.isLoading) return <LoadingState label="Loading your vehicle..." />;
  if (vehicle.isError) return <ErrorState title="Vehicle unavailable" message={vehicle.error.message} onRetry={() => void vehicle.refetch()} />;

  return (
    <VehicleFormScreen
      key={vehicle.data?.updatedAt ?? 'new-vehicle'}
      currentVehicle={vehicle.data ?? null}
      onBack={onBack}
      onOpenDocuments={onOpenDocuments}
    />
  );
}

function VehicleFormScreen({ currentVehicle, onBack, onOpenDocuments }: { currentVehicle: DriverVehicle | null; onBack(): void; onOpenDocuments(): void }) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<VehicleForm>(() => currentVehicle ? {
    make: currentVehicle.make,
    model: currentVehicle.model,
    year: String(currentVehicle.year),
    color: currentVehicle.color,
    registrationNumber: currentVehicle.registrationNumber,
    vehicleIdentificationNumber: currentVehicle.vehicleIdentificationNumber,
    seatCapacity: String(currentVehicle.seatCapacity),
  } : emptyForm);
  const [submitted, setSubmitted] = useState(false);

  const saveVehicle = useMutation({
    mutationFn: (input: UpdateDriverVehicleInput) => saveDriverVehicle(input),
    onSuccess: (result) => queryClient.setQueryData(driverVehicleQuery.queryKey, result),
  });
  const validation = validateVehicle(form);
  const errorMessage = saveVehicle.error
    ? isApiError(saveVehicle.error)
      ? saveVehicle.error.problem?.detail ?? saveVehicle.error.message
      : 'Vehicle details could not be saved.'
    : undefined;

  function update(key: keyof VehicleForm, value: string) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function submit() {
    setSubmitted(true);
    saveVehicle.reset();
    if (!validation.valid) return;
    saveVehicle.mutate({
      make: form.make.trim(),
      model: form.model.trim(),
      year: Number(form.year),
      color: form.color.trim(),
      registrationNumber: form.registrationNumber.trim().toUpperCase(),
      vehicleIdentificationNumber: form.vehicleIdentificationNumber.trim().toUpperCase(),
      seatCapacity: Number(form.seatCapacity),
    });
  }

  return (
    <KeyboardAwareScrollView
      bottomOffset={spacing.xl}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.lg, paddingBottom: 116 + insets.bottom }]}
      contentInsetAdjustmentBehavior="automatic"
      keyboardDismissMode="interactive"
      keyboardShouldPersistTaps="handled"
      mode="insets"
    >
      <View style={styles.header}>
        <Pressable accessibilityLabel="Back to Driver account" accessibilityRole="button" onPress={onBack} style={({ pressed }) => [styles.backButton, pressed && styles.pressed]}>
          <RydoIcon name="chevron-left" color={colors.navy} size={23} />
        </Pressable>
        <View style={styles.headerCopy}>
          <Text selectable style={styles.title}>Your vehicle</Text>
          <Text selectable style={styles.subtitle}>Add the vehicle you will use for RYDO trips.</Text>
        </View>
      </View>

      <View style={styles.heroCard}>
        <View style={styles.heroIcon}><DriverRideIcon color={colors.white} size={30} /></View>
        <View style={styles.heroCopy}>
          <Text style={styles.heroTitle}>{currentVehicle ? 'Vehicle on file' : 'Vehicle verification'}</Text>
          <Text selectable style={styles.heroText}>{currentVehicle ? formatReviewStatus(currentVehicle.reviewStatus) : 'Complete every field exactly as it appears on the vehicle documents.'}</Text>
        </View>
      </View>

      <View style={styles.formCard}>
        <View style={styles.twoColumns}>
          <VehicleField error={submitted ? validation.make : undefined} label="Make" onChangeText={(value) => update('make', value)} placeholder="Toyota" value={form.make} />
          <VehicleField error={submitted ? validation.model : undefined} label="Model" onChangeText={(value) => update('model', value)} placeholder="Corolla" value={form.model} />
        </View>
        <View style={styles.twoColumns}>
          <VehicleField error={submitted ? validation.year : undefined} keyboardType="number-pad" label="Year" maxLength={4} onChangeText={(value) => update('year', value.replace(/\D/g, ''))} placeholder="2022" value={form.year} />
          <VehicleField error={submitted ? validation.color : undefined} label="Colour" onChangeText={(value) => update('color', value)} placeholder="White" value={form.color} />
        </View>
        <VehicleField autoCapitalize="characters" error={submitted ? validation.registrationNumber : undefined} label="Registration number" maxLength={16} onChangeText={(value) => update('registrationNumber', value)} placeholder="CA 123-456" value={form.registrationNumber} />
        <VehicleField autoCapitalize="characters" error={submitted ? validation.vehicleIdentificationNumber : undefined} label="VIN" maxLength={17} onChangeText={(value) => update('vehicleIdentificationNumber', value.replace(/\s/g, ''))} placeholder="17-character vehicle identification number" value={form.vehicleIdentificationNumber} />
        <VehicleField error={submitted ? validation.seatCapacity : undefined} keyboardType="number-pad" label="Passenger seat capacity" maxLength={2} onChangeText={(value) => update('seatCapacity', value.replace(/\D/g, ''))} placeholder="4" value={form.seatCapacity} />
      </View>

      {currentVehicle?.rejectionReason ? <MessageCard danger message={currentVehicle.rejectionReason} /> : null}
      {errorMessage ? <MessageCard danger message={errorMessage} /> : null}
      {saveVehicle.isSuccess ? <MessageCard message="Vehicle details saved successfully." /> : null}

      <Pressable accessibilityRole="button" accessibilityState={{ busy: saveVehicle.isPending }} disabled={saveVehicle.isPending} onPress={submit} style={({ pressed }) => [styles.primaryButton, pressed && styles.primaryPressed]}>
        {saveVehicle.isPending ? <ActivityIndicator color={colors.white} /> : <><Text style={styles.primaryLabel}>Save vehicle</Text><RydoIcon name="check" color={colors.white} size={18} /></>}
      </Pressable>

      {currentVehicle || saveVehicle.isSuccess ? (
        <Pressable accessibilityRole="button" onPress={onOpenDocuments} style={({ pressed }) => [styles.secondaryButton, pressed && styles.pressed]}>
          <Text style={styles.secondaryLabel}>Continue to Driver documents</Text>
          <RydoIcon name="chevron-right" color={colors.blue} size={19} />
        </Pressable>
      ) : null}
    </KeyboardAwareScrollView>
  );
}

function VehicleField({ error, label, ...props }: {
  autoCapitalize?: 'characters' | 'words'; error?: string; keyboardType?: 'default' | 'number-pad'; label: string; maxLength?: number; onChangeText(value: string): void; placeholder: string; value: string;
}) {
  return <View style={styles.field}><Text style={styles.fieldLabel}>{label}</Text><TextInput {...props} accessibilityLabel={label} autoCapitalize={props.autoCapitalize ?? 'words'} placeholderTextColor={colors.textMuted} selectionColor={colors.blue} style={[styles.input, error && styles.inputError]} />{error ? <Text selectable style={styles.fieldError}>{error}</Text> : null}</View>;
}

function MessageCard({ danger = false, message }: { danger?: boolean; message: string }) {
  return <View style={[styles.messageCard, danger && styles.messageDanger]}><RydoIcon name={danger ? 'error' : 'check'} color={danger ? colors.danger : colors.success} size={18} /><Text selectable style={[styles.messageText, danger && styles.messageTextDanger]}>{message}</Text></View>;
}

function formatReviewStatus(status: 'PendingReview' | 'Approved' | 'Rejected') {
  if (status === 'Approved') return 'Approved and ready for onboarding.';
  if (status === 'Rejected') return 'Needs correction before approval.';
  return 'Pending review. You can update it while onboarding remains editable.';
}

function validateVehicle(form: VehicleForm) {
  const year = Number(form.year);
  const seats = Number(form.seatCapacity);
  const make = form.make.trim().length ? undefined : 'Make is required.';
  const model = form.model.trim().length ? undefined : 'Model is required.';
  const color = form.color.trim().length ? undefined : 'Colour is required.';
  const yearError = !Number.isInteger(year) || year < 1980 || year > 2100 ? 'Use a year from 1980 to 2100.' : undefined;
  const registration = /^[A-Za-z0-9 -]{2,16}$/.test(form.registrationNumber.trim()) ? undefined : 'Use 2–16 letters, numbers, spaces, or hyphens.';
  const vin = /^[A-HJ-NPR-Za-hj-npr-z0-9]{17}$/.test(form.vehicleIdentificationNumber.trim()) ? undefined : 'Enter a valid 17-character VIN.';
  const seatCapacity = !Number.isInteger(seats) || seats < 1 || seats > 16 ? 'Enter between 1 and 16 seats.' : undefined;
  return { valid: !make && !model && !color && !yearError && !registration && !vin && !seatCapacity, make, model, color, year: yearError, registrationNumber: registration, vehicleIdentificationNumber: vin, seatCapacity };
}

const styles = StyleSheet.create({
  content: { gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  backButton: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 15, backgroundColor: colors.white },
  headerCopy: { minWidth: 0, flex: 1, gap: 3 },
  title: { color: colors.navy, fontSize: 30, lineHeight: 36, fontWeight: '900', letterSpacing: -0.8 },
  subtitle: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
  heroCard: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.blue },
  heroIcon: { width: 54, height: 54, alignItems: 'center', justifyContent: 'center', borderRadius: 18, backgroundColor: 'rgba(255,255,255,0.16)' },
  heroCopy: { minWidth: 0, flex: 1, gap: 4 },
  heroTitle: { color: colors.white, fontSize: 17, fontWeight: '900' },
  heroText: { color: colors.white, fontSize: 12, lineHeight: 17 },
  formCard: { gap: spacing.lg, padding: spacing.lg, borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  twoColumns: { flexDirection: 'row', gap: spacing.md },
  field: { minWidth: 0, flex: 1, gap: spacing.sm },
  fieldLabel: { color: colors.navy, fontSize: 12, fontWeight: '800' },
  input: { minHeight: 54, paddingHorizontal: spacing.md, borderWidth: 1, borderColor: colors.border, borderRadius: driverTheme.radii.control, backgroundColor: colors.white, color: colors.navy, fontSize: 15 },
  inputError: { borderColor: colors.danger, backgroundColor: colors.dangerMuted },
  fieldError: { color: colors.danger, fontSize: 10, lineHeight: 14 },
  messageCard: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.sm, padding: spacing.md, borderRadius: driverTheme.radii.control, backgroundColor: colors.successMuted },
  messageDanger: { backgroundColor: colors.dangerMuted },
  messageText: { minWidth: 0, flex: 1, color: colors.success, fontSize: 12, lineHeight: 18 },
  messageTextDanger: { color: colors.danger },
  primaryButton: { minHeight: 58, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderRadius: driverTheme.radii.button, backgroundColor: colors.blue },
  primaryPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  primaryLabel: { color: colors.white, fontSize: 16, fontWeight: '900' },
  secondaryButton: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderWidth: 1, borderColor: colors.blue, borderRadius: driverTheme.radii.button, backgroundColor: colors.white },
  secondaryLabel: { color: colors.blue, fontSize: 14, fontWeight: '900' },
  pressed: { opacity: 0.68, transform: [{ scale: 0.98 }] },
});
