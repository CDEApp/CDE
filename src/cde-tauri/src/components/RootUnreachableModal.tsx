import { Button, Group, Modal, Text } from "@mantine/core";

interface Props {
  /** The catalog root that could not be reached, or null when the modal is closed. */
  rootPath: string | null;
  onClose: () => void;
}

/** Shown when a rescan is refused because the catalog's root path is missing or unreadable. */
export function RootUnreachableModal({ rootPath, onClose }: Props) {
  return (
    <Modal opened={rootPath !== null} onClose={onClose} title="Cannot rescan catalog" centered>
      <Text>
        <b>{rootPath}</b> can't be reached (drive disconnected, path moved, or no permission). The catalog was
        left unchanged.
      </Text>
      <Group justify="flex-end" mt="md">
        <Button onClick={onClose} data-autofocus>
          OK
        </Button>
      </Group>
    </Modal>
  );
}
