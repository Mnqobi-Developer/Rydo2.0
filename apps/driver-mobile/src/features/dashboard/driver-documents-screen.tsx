import { isApiError } from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { driverTheme } from '@/theme/driver-theme';

import {
  driverDocumentsQuery,
  driverProfileQuery,
  driverVehicleQuery,
  submitDriverOnboarding,
  uploadDriverDocument,
  type DriverDocument,
  type UploadDriverDocumentInput,
} from './api';

type DocumentType = DriverDocument['documentType'];

const requiredDocuments: { type: DocumentType; title: string; description: string }[] = [
  { type: 'IdentityDocument', title: 'Identity document', description: 'South African ID or accepted identity document.' },
  { type: 'DriversLicense', title: "Driver's licence", description: 'A clear copy of your valid driving licence.' },
  { type: 'ProfessionalDrivingPermit', title: 'Professional Driving Permit', description: 'Your current PrDP document.' },
];

export function DriverDocumentsScreen({
  onBack,
  onOpenVehicle,
  onSubmitted,
}: {
  onBack(): void;
  onOpenVehicle(): void;
  onSubmitted(): void;
}) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const documents = useQuery(driverDocumentsQuery);
  const vehicle = useQuery(driverVehicleQuery);
  const profile = useQuery({ ...driverProfileQuery, retry: false });
  const [uploadingType, setUploadingType] = useState<DocumentType | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);

  const uploadDocument = useMutation({
    mutationFn: (input: UploadDriverDocumentInput) => uploadDriverDocument(input),
    onSuccess: (result) => {
      setLocalError(null);
      queryClient.setQueryData<DriverDocument[]>(driverDocumentsQuery.queryKey, (current = []) => [
        ...current.filter((document) => document.documentType !== result.documentType),
        result,
      ]);
      void queryClient.invalidateQueries({ queryKey: driverProfileQuery.queryKey });
    },
  });
  const submitOnboarding = useMutation({
    mutationFn: submitDriverOnboarding,
    onSuccess: (result) => {
      setLocalError(null);
      queryClient.setQueryData(driverProfileQuery.queryKey, result);
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: driverProfileQuery.queryKey }),
        queryClient.invalidateQueries({ queryKey: driverDocumentsQuery.queryKey }),
        queryClient.invalidateQueries({ queryKey: driverVehicleQuery.queryKey }),
      ]);
      Alert.alert(
        'Documents received',
        'Your documents and onboarding details have been received and are pending approval.',
        [{ text: 'Back to account', onPress: onSubmitted }],
        { cancelable: false },
      );
    },
  });

  const registered = new Map((documents.data ?? []).map((document) => [document.documentType, document]));
  const validDocumentCount = requiredDocuments.filter(({ type }) =>
    registered.get(type)?.reviewStatus !== 'Rejected' && registered.has(type)).length;
  const allDocumentsPresent = validDocumentCount === requiredDocuments.length;
  const canSubmit = Boolean(vehicle.data) && allDocumentsPresent && profile.data?.canEdit === true;
  const refreshing = documents.isRefetching || vehicle.isRefetching || profile.isRefetching;
  const mutationError = uploadDocument.error ?? submitOnboarding.error;
  const errorMessage = localError ?? (mutationError
    ? isApiError(mutationError)
      ? mutationError.problem?.detail ?? mutationError.message
      : 'The onboarding request could not be completed.'
    : null);

  async function chooseDocument(documentType: DocumentType) {
    setLocalError(null);
    uploadDocument.reset();
    setUploadingType(documentType);
    try {
      const DocumentPicker = await loadDocumentPicker();
      const result = await DocumentPicker.getDocumentAsync({
        copyToCacheDirectory: true,
        multiple: false,
        type: ['application/pdf', 'image/jpeg', 'image/png'],
      });
      if (result.canceled) return;
      const asset = result.assets[0];
      const contentType = supportedContentType(asset.mimeType, asset.name);
      if (!contentType) throw new Error('Choose a PDF, JPEG, or PNG document.');

      if (asset.size != null && (asset.size < 1 || asset.size > 10 * 1024 * 1024)) {
        throw new Error('Documents must contain data and be no larger than 10 MB.');
      }

      await uploadDocument.mutateAsync({
        documentType,
        file: {
          uri: asset.uri,
          name: sanitizeFileName(asset.name),
          type: contentType,
        },
      });
    } catch (error) {
      setLocalError(error instanceof Error ? error.message : 'The document could not be processed.');
    } finally {
      setUploadingType(null);
    }
  }

  function refresh() {
    setLocalError(null);
    uploadDocument.reset();
    submitOnboarding.reset();
    void Promise.all([documents.refetch(), vehicle.refetch(), profile.refetch()]);
  }

  return (
    <ScrollView
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.lg, paddingBottom: 116 + insets.bottom }]}
      contentInsetAdjustmentBehavior="automatic"
      refreshControl={<RefreshControl refreshing={refreshing} tintColor={colors.blue} onRefresh={refresh} />}
      showsVerticalScrollIndicator={false}
    >
      <View style={styles.header}>
        <Pressable accessibilityLabel="Back to Driver account" accessibilityRole="button" onPress={onBack} style={({ pressed }) => [styles.backButton, pressed && styles.pressed]}>
          <RydoIcon name="chevron-left" color={colors.navy} size={23} />
        </Pressable>
        <View style={styles.headerCopy}><Text selectable style={styles.title}>Driver documents</Text><Text selectable style={styles.subtitle}>Register the three documents required by the RYDO API.</Text></View>
      </View>

      <View style={styles.progressCard}>
        <View style={styles.progressIcon}><RydoIcon name="shield" color={colors.white} size={24} /></View>
        <View style={styles.progressCopy}>
          <Text style={styles.progressTitle}>{validDocumentCount} of {requiredDocuments.length} documents ready</Text>
          <Text selectable style={styles.progressText}>{vehicle.data ? 'Vehicle added' : 'Vehicle details are still required'}</Text>
        </View>
      </View>

      <View style={styles.documentList}>
        {requiredDocuments.map((requirement) => (
          <DocumentCard
            key={requirement.type}
            description={requirement.description}
            document={registered.get(requirement.type)}
            loading={uploadingType === requirement.type}
            onChoose={() => void chooseDocument(requirement.type)}
            title={requirement.title}
          />
        ))}
      </View>

      {!vehicle.data ? (
        <Pressable accessibilityRole="button" onPress={onOpenVehicle} style={({ pressed }) => [styles.vehicleReminder, pressed && styles.pressed]}>
          <RydoIcon name="error" color={colors.amber} size={19} />
          <View style={styles.reminderCopy}><Text style={styles.reminderTitle}>Vehicle details required</Text><Text selectable style={styles.reminderText}>Add your vehicle before submitting onboarding.</Text></View>
          <RydoIcon name="chevron-right" color={colors.amber} size={18} />
        </Pressable>
      ) : null}

      <View style={styles.storageNotice}>
        <RydoIcon name="shield" color={colors.blue} size={18} />
        <Text selectable style={styles.storageText}>Documents are uploaded through the authenticated RYDO API and kept in protected private storage. PDF, JPEG, and PNG files up to 10 MB are accepted.</Text>
      </View>

      {errorMessage ? <View style={styles.errorCard}><RydoIcon name="error" color={colors.danger} size={18} /><Text selectable style={styles.errorText}>{errorMessage}</Text></View> : null}
      {submitOnboarding.isSuccess ? <View style={styles.successCard}><RydoIcon name="check" color={colors.success} size={18} /><Text selectable style={styles.successText}>Documents received and pending approval.</Text></View> : null}

      <Pressable
        accessibilityRole="button"
        accessibilityState={{ busy: submitOnboarding.isPending, disabled: !canSubmit || submitOnboarding.isPending }}
        disabled={!canSubmit || submitOnboarding.isPending}
        onPress={() => submitOnboarding.mutate()}
        style={({ pressed }) => [styles.submitButton, (!canSubmit || submitOnboarding.isPending) && styles.submitDisabled, pressed && canSubmit && styles.submitPressed]}
      >
        {submitOnboarding.isPending ? <ActivityIndicator color={colors.white} /> : <><Text style={styles.submitLabel}>Submit onboarding for review</Text><RydoIcon name="chevron-right" color={colors.white} size={20} /></>}
      </Pressable>
    </ScrollView>
  );
}

function DocumentCard({ description, document, loading, onChoose, title }: { description: string; document?: DriverDocument; loading: boolean; onChoose(): void; title: string }) {
  const rejected = document?.reviewStatus === 'Rejected';
  return (
    <View style={styles.documentCard}>
      <View style={styles.documentTop}>
        <View style={styles.documentIcon}><RydoIcon name="bookmark" color={colors.blue} size={21} /></View>
        <View style={styles.documentCopy}><Text style={styles.documentTitle}>{title}</Text><Text selectable style={styles.documentDescription}>{description}</Text></View>
        {document ? <StatusChip status={document.reviewStatus} /> : null}
      </View>
      {document ? <Text numberOfLines={1} selectable style={styles.fileName}>{document.originalFileName} · {formatBytes(document.sizeBytes)}</Text> : null}
      {document?.rejectionReason ? <Text selectable style={styles.rejectionText}>{document.rejectionReason}</Text> : null}
      {!document || rejected ? (
        <Pressable accessibilityRole="button" disabled={loading} onPress={onChoose} style={({ pressed }) => [styles.chooseButton, loading && styles.chooseDisabled, pressed && styles.pressed]}>
          {loading ? <ActivityIndicator color={colors.blue} size="small" /> : <><RydoIcon name="upload" color={colors.blue} size={18} /><Text style={styles.chooseLabel}>{document ? 'Upload replacement' : 'Choose document'}</Text></>}
        </Pressable>
      ) : null}
    </View>
  );
}

function StatusChip({ status }: { status: DriverDocument['reviewStatus'] }) {
  const approved = status === 'Approved';
  const rejected = status === 'Rejected';
  return <View style={[styles.chip, approved && styles.chipApproved, rejected && styles.chipRejected]}><Text style={[styles.chipText, approved && styles.chipTextApproved, rejected && styles.chipTextRejected]}>{approved ? 'Approved' : rejected ? 'Review' : 'Pending'}</Text></View>;
}

function supportedContentType(value: string | undefined, fileName: string): UploadDriverDocumentInput['file']['type'] | null {
  if (value === 'application/pdf' || value === 'image/jpeg' || value === 'image/png') return value;
  const lower = fileName.toLowerCase();
  if (lower.endsWith('.pdf')) return 'application/pdf';
  if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) return 'image/jpeg';
  if (lower.endsWith('.png')) return 'image/png';
  return null;
}

function sanitizeFileName(value: string) { return value.replace(/[\\/]/g, '-').slice(0, 255) || 'driver-document'; }
function formatBytes(value: number) { return value < 1024 * 1024 ? `${Math.max(1, Math.round(value / 1024))} KB` : `${(value / (1024 * 1024)).toFixed(1)} MB`; }

async function loadDocumentPicker() {
  try {
    return await import('expo-document-picker');
  } catch (error) {
    const message = error instanceof Error ? error.message : '';
    if (message.includes('ExpoDocumentPicker')) {
      throw new Error(
        'Document selection is not available in this installed app. Update Expo Go to the SDK 57 version, or install a new RYDO Driver development build.',
      );
    }

    throw error;
  }
}

const styles = StyleSheet.create({
  content: { gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  backButton: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 15, backgroundColor: colors.white },
  headerCopy: { minWidth: 0, flex: 1, gap: 3 },
  title: { color: colors.navy, fontSize: 29, lineHeight: 35, fontWeight: '900', letterSpacing: -0.7 },
  subtitle: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
  progressCard: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.blue },
  progressIcon: { width: 50, height: 50, alignItems: 'center', justifyContent: 'center', borderRadius: 17, backgroundColor: 'rgba(255,255,255,0.16)' },
  progressCopy: { minWidth: 0, flex: 1, gap: 4 },
  progressTitle: { color: colors.white, fontSize: 16, fontWeight: '900' },
  progressText: { color: colors.white, fontSize: 12 },
  documentList: { gap: spacing.md },
  documentCard: { gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  documentTop: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  documentIcon: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 15, backgroundColor: colors.blueMuted },
  documentCopy: { minWidth: 0, flex: 1, gap: 4 },
  documentTitle: { color: colors.navy, fontSize: 15, fontWeight: '900' },
  documentDescription: { color: colors.textMuted, fontSize: 11, lineHeight: 16 },
  fileName: { color: colors.navy, fontSize: 11, fontWeight: '700' },
  rejectionText: { color: colors.danger, fontSize: 11, lineHeight: 16 },
  chip: { paddingHorizontal: 8, paddingVertical: 5, borderRadius: 999, backgroundColor: colors.amberMuted },
  chipApproved: { backgroundColor: colors.successMuted },
  chipRejected: { backgroundColor: colors.dangerMuted },
  chipText: { color: colors.amber, fontSize: 9, fontWeight: '900' },
  chipTextApproved: { color: colors.success },
  chipTextRejected: { color: colors.danger },
  chooseButton: { minHeight: 46, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderWidth: 1, borderColor: colors.blue, borderRadius: 15, backgroundColor: colors.white },
  chooseDisabled: { opacity: 0.55 },
  chooseLabel: { color: colors.blue, fontSize: 13, fontWeight: '900' },
  vehicleReminder: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.control, backgroundColor: colors.amberMuted },
  reminderCopy: { minWidth: 0, flex: 1, gap: 3 },
  reminderTitle: { color: colors.navy, fontSize: 13, fontWeight: '900' },
  reminderText: { color: colors.textMuted, fontSize: 11 },
  storageNotice: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.sm, padding: spacing.md, borderRadius: driverTheme.radii.control, backgroundColor: colors.blueMuted },
  storageText: { minWidth: 0, flex: 1, color: colors.textMuted, fontSize: 11, lineHeight: 17 },
  errorCard: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.sm, padding: spacing.md, borderRadius: driverTheme.radii.control, backgroundColor: colors.dangerMuted },
  errorText: { minWidth: 0, flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  successCard: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, padding: spacing.md, borderRadius: driverTheme.radii.control, backgroundColor: colors.successMuted },
  successText: { color: colors.success, fontSize: 12, fontWeight: '800' },
  submitButton: { minHeight: 58, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderRadius: driverTheme.radii.button, backgroundColor: colors.blue },
  submitDisabled: { opacity: 0.42 },
  submitPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  submitLabel: { color: colors.white, fontSize: 15, fontWeight: '900' },
  pressed: { opacity: 0.68, transform: [{ scale: 0.98 }] },
});
