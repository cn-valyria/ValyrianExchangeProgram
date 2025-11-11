import { LOAD_ACCOUNT, LOAD_LIST, LOAD_TRANSACTIONS } from "./mutation-types";

export const state = () => ({
  account: {},
  aidList: [],
  transactionHistory: []
});

export const mutations = {
  [LOAD_ACCOUNT](state, payload) {
    state.account = payload;
  },
  [LOAD_LIST](state, payload) {
    state.aidList = payload;
  },
  [LOAD_TRANSACTIONS](state, payload) {
    state.transactionHistory = payload;
  }
};

export const actions = {
  async loadAccount(context, accountId) {
    const response = await this.$axios.get(`accounts/${accountId}`);
    this.$log.debug(response);
    context.commit(LOAD_ACCOUNT, response.data);
  },
  async loadAidList(context, accountId) {
    const response = await this.$axios.get(`accounts/${accountId}/aidList`);
    this.$log.debug(response);
    context.commit(LOAD_LIST, response.data);
  },
  async loadTransactionHistory(context, accountId) {
    const response = await this.$axios.get(`accounts/${accountId}/transactions`);
    this.$log.debug(response);
    context.commit(LOAD_TRANSACTIONS, response.data);
  }
};

export const getters = {
  creditHistory: state => {
    const account = state.account;
    if (account.nationId === undefined) {
      return [];
    }
    
    const transactions = state.transactionHistory;
    return transactions.map(txn => ({
      id: txn.id,
      otherNation: buildOtherNation(txn, account),
      type: buildTransactionType(txn, account),
      status: txn.status,
      rate: txn.rate,
      money: txn.aidId === null ? null : txn.money,
      technology: txn.aidId === null ? null : txn.technology,
      balanceChangeType: buildBalanceChangeType(txn, account),
      sentOn: txn.startsOn
    }));
  }
};

function buildOtherNation(txn, account) {
  if (txn.aidId === null) {
    return null;
  } else if (txn.sentBy.nationId !== account.nationId) {
    return txn.sentBy;
  } else if (txn.receivedBy.nationId !== account.nationId) {
    return txn.receivedBy;
  } else {
    throw new Error("Unable to find a valid nation for the given transaction and account.");
  }
}

function buildTransactionType(txn, account) {
  const incoming = "Incoming";
  const outgoing = "Outgoing";

  if (txn.aidId === null) {
    return txn.sentBy !== null ? outgoing : incoming;
  } else if (txn.sentBy.nationId === account.nationId) {
    return outgoing;
  } else if (txn.receivedBy.nationId === account.nationId) {
    return incoming;
  } else {
    throw new Error("Unable to determine the transaction type for the given transaction and account.");
  }
}

function buildBalanceChangeType(txn, account) {
  const credit = "Credit";
  const debt = "Debt";

  if (txn.aidId === null) {
    return txn.sentBy !== null ? credit : debt;
  } else if (txn.sentBy.nationId === account.nationId) {
    return credit;
  } else if (txn.receivedBy.nationId === account.nationId) {
    return debt;
  } else {
    throw new Error("Unable to determine the balance change type for the given transaction and account.");
  }
}