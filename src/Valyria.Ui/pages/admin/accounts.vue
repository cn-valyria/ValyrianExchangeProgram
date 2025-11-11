<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Accounts</h1>
        <h2 class="subtitle">Manage accounts enrolled in VEP, or enroll new accounts</h2>
      </div>
    </div>
    <div class="container">
      <div class="section table-container">
        <Button label="Create" severity="success" @click="createAccount()" class="mb-3" />
        <DataTable
          :value="accounts"
          :sortField="'nationId'"
          :sortOrder="1"
          filterDisplay="row"
          :globalFilterFields="['rulerName', 'nationName', 'allianceName', 'role']"
          paginator
          :rows="25"
          :rowsPerPageOptions="[10, 25, 50, 100]"
        >
          <Column field="id" header="Actions" style="width: 140px">
            <template #body="slotProps">
              <div class="flex gap-2">
                <Button
                  label="Edit"
                  severity="info"
                  size="small"
                  @click="editAccount(slotProps.data)"
                />
                <Button
                  label="Delete"
                  severity="danger"
                  size="small"
                  :loading="accountIdBeingDeleted === slotProps.data.id"
                  @click="deleteAccountById(slotProps.data.id)"
                />
              </div>
            </template>
          </Column>
          <Column field="nationId" header="Nation" sortable>
            <template #body="slotProps">
              {{ slotProps.data.rulerName }} of {{ slotProps.data.nationName }}
            </template>
            <template #filter="{ filterModel, filterCallback }">
              <InputText
                v-model="filterModel.value"
                type="text"
                @input="filterCallback()"
                placeholder="Filter..."
                class="p-column-filter"
              />
            </template>
          </Column>
          <Column field="allianceName" header="Alliance" sortable>
            <template #body="slotProps">
              {{ slotProps.data.allianceName }}
            </template>
            <template #filter="{ filterModel, filterCallback }">
              <InputText
                v-model="filterModel.value"
                type="text"
                @input="filterCallback()"
                placeholder="Filter..."
                class="p-column-filter"
              />
            </template>
          </Column>
          <Column field="role" header="Role" sortable>
            <template #body="slotProps">
              {{ getRoleDescription(slotProps.data.role) }}
            </template>
            <template #filter="{ filterModel, filterCallback }">
              <InputText
                v-model="filterModel.value"
                type="text"
                @input="filterCallback()"
                placeholder="Filter..."
                class="p-column-filter"
              />
            </template>
          </Column>
          <Column field="credit" header="Credit" sortable>
            <template #body="slotProps">
              {{ slotProps.data.credit }}
            </template>
          </Column>
          <Column field="debt" header="Debt" sortable>
            <template #body="slotProps">
              {{ slotProps.data.debt }}
            </template>
          </Column>
          <Column field="slotsUsed" header="Slots Free" sortable>
            <template #body="slotProps">
              {{ slotProps.data.slotsFull - slotProps.data.slotsUsed }}
            </template>
          </Column>
          <Column field="slotsFull" header="Slots Full" sortable>
            <template #body="slotProps">
              {{ slotProps.data.slotsFull }}
            </template>
          </Column>
        </DataTable>
      </div>
    </div>

    <AdminAccountEditDialog
      :account="accountBeingEdited"
      :show="editDialogIsVisible"
      @close="closeAccountEditDialog()"
    />
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAdminAccountsStore } from '~/stores/adminAccounts'
import { useAuthStore } from '~/stores/auth'
import { accountRoles } from '~/infrastructure/dataLists'

interface Account {
  id: number
  nationId: number
  rulerName: string
  nationName: string
  role: string
  allianceId: number
  allianceName: string
  allianceAcronym: string
  uniqueCode: string
  discord: string | null
  discordUniqueId: string | null
  credit: number
  debt: number
  slotsUsed: number
  slotsFull: number
  hasForeignMinistry: boolean
  hasFederalAidCommission: boolean
  hasDisasterReliefAgency: boolean
}

definePageMeta({
  middleware: 'auth'
})

const router = useRouter()
const toast = useToast()

const adminAccountsStore = useAdminAccountsStore()
const authStore = useAuthStore()

const { accounts } = storeToRefs(adminAccountsStore)
const { loggedInUser } = storeToRefs(authStore)

const editDialogIsVisible = ref<boolean>(false)
const accountBeingEdited = ref<Account | undefined>(undefined)
const accountIdBeingDeleted = ref<number>(0)
const roles = accountRoles

// Helper function to get role description
const getRoleDescription = (roleCode: string): string => {
  const role = roles.find(r => r.code === roleCode)
  return role ? role.description : roleCode
}

// Filter function for nation search (ruler or nation name)
const filterByNation = (row: Account, input: string): boolean => {
  return row.rulerName.toLowerCase().includes(input.toLowerCase())
    || row.nationName.toLowerCase().includes(input.toLowerCase())
}

// Filter function for role search
const filterByRole = (row: Account, input: string): boolean => {
  const rolesMatchingInput = roles
    .filter(role => role.description.toLowerCase().includes(input.toLowerCase()))
    .map(role => role.code)

  return rolesMatchingInput.includes(row.role)
}

// Create new account
const createAccount = (): void => {
  accountBeingEdited.value = {} as Account
  editDialogIsVisible.value = true
}

// Edit existing account
const editAccount = (account: Account): void => {
  accountBeingEdited.value = account
  editDialogIsVisible.value = true
}

// Delete account by ID
const deleteAccountById = async (accountId: number): Promise<void> => {
  accountIdBeingDeleted.value = accountId

  try {
    await adminAccountsStore.deleteAccount(accountId)
    toast.add({
      severity: 'success',
      summary: 'Success',
      detail: 'Account deleted successfully.',
      life: 3000
    })
  } catch (error) {
    console.error('Failed to delete account:', error)
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'VEP encountered an error while deleting the account. Please try again, or contact @lilweirdward if the error persists.',
      life: 5000
    })
  } finally {
    accountIdBeingDeleted.value = 0
  }
}

// Close account edit dialog
const closeAccountEditDialog = (): void => {
  accountBeingEdited.value = undefined
  editDialogIsVisible.value = false
}

// Check admin access and load accounts on mount
onMounted(async () => {
  // Check if user has admin role
  if (!loggedInUser.value?.roles?.some((role: any) => role.name === 'Admin')) {
    console.warn('Tried to access an admin page without the admin role.')
    toast.add({
      severity: 'error',
      summary: 'Access Denied',
      detail: 'You do not have permission to access this page.',
      life: 5000
    })
    await router.push('/')
    return
  }

  try {
    await adminAccountsStore.loadAllAccounts()
    console.log('Accounts loaded:', accounts.value)
  } catch (error) {
    console.error('Failed to load accounts:', error)
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'Failed to load accounts. Please try again.',
      life: 5000
    })
  }
})
</script>
