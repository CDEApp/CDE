import { Button, Code, Group, Modal, Table, Text } from "@mantine/core";
import { EVERYTHING_HELP, EVERYTHING_HELP_NOT_SUPPORTED } from "../everythingHelp";

interface Props {
  opened: boolean;
  onClose: () => void;
}

/** Help > Search Syntax: the Everything-style operators, modifiers and filters, with examples. */
export function SearchSyntaxModal({ opened, onClose }: Props) {
  return (
    <Modal opened={opened} onClose={onClose} title="Search Syntax (Everything mode)" centered size={920} styles={{ body: { minHeight: "70vh" } }}>
      {EVERYTHING_HELP.map((section) => (
        <div key={section.title}>
          <Text fw={700} mt="sm">
            {section.title}
          </Text>
          <Table withRowBorders={false} verticalSpacing={2}>
            <Table.Tbody>
              {section.rows.map((row) => (
                <Table.Tr key={row.syntax}>
                  <Table.Td w="40%">
                    <Code>{row.syntax}</Code>
                  </Table.Td>
                  <Table.Td>{row.meaning}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </div>
      ))}
      {EVERYTHING_HELP_NOT_SUPPORTED.map((line) => (
        <Text key={line} size="sm" c="dimmed" mt="xs">
          {line}
        </Text>
      ))}
      <Group justify="flex-end" mt="md">
        <Button onClick={onClose} data-autofocus>
          Close
        </Button>
      </Group>
    </Modal>
  );
}
